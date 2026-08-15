using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FanucRobotInterface.Common.Configuration;
using FanucRobotInterface.Common.Exceptions;

namespace FanucRobotInterface.Common;

/// <summary>
/// FANUC SNPX 协议传输层：TCP 连接、SNPX 帧构造/收发、握手与序号管理。
/// </summary>
public class SnpxProtocol : IDisposable
{
    // SNPX 协议常量
    private const int HeaderSize = 56;          // 固定帧头长度
    private const int DefaultSnpxPort = 60008;   // 默认端口

    // SNPX 帧 function 常量（selector）
    internal const byte FunctionReadWriteReg = 8;   // 读/写数值寄存器 D（16 位字）
    internal const byte FunctionWriteCommand = 56;  // 写命令字符串（KAREL 风格，如 SETASG）
    internal const byte SelectorDigitalRead = 72;   // 位信号读（Q 区）
    internal const byte SelectorDigitalWrite = 70;  // 位信号写（I 区）
    internal const byte SelectorAnalogInput = 12;   // 模拟输入 AI = 12
    internal const byte SelectorAnalogOutput = 10;  // 模拟输出 AO = 10
    internal const byte SelectorGroupInput = 12;    // 组输入 GI = 12
    internal const byte SelectorGroupOutput = 10;   // 组输出 GO = 10
    internal const byte SelectorPmcSignal = 76;     // PMC 继电器 M/R/K = 76
    internal const byte SelectorPmcData = 10;       // PMC 数据 D = 10

    // 帧模板（56 字节，SNPX 协议固定帧头）
    // 连接帧：全零 + [1..4]=ClientId
    private static readonly byte[] FrameConnect = new byte[56];

    // 会话帧（session_req）：[0]=8
    private static readonly byte[] FrameSession = {
        8, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0xC0, 0, 0, 0, 0, 0x10, 0x0E, 0, 0,
        1, 1, 0x4F, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    // 读帧模板（BulidReadData）：[2]=6，sel/address/length 在 [43..47]
    private static readonly byte[] FrameRead = {
        2, 0, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0xC0, 0, 0, 0, 0, 0x10, 0x0E, 0, 0,
        1, 1, 4, 8, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    // 写帧模板（小数据，value.Length<=6）：[2]=8，sel/address/length 在 [43..47]，data 从 [48]
    private static readonly byte[] FrameWriteSmall = {
        2, 0, 8, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0xC0, 0, 0, 0, 0, 0x10, 0x0E, 0, 0,
        1, 1, 7, 8, 9, 0, 4, 0, 1, 0, 2, 0, 3, 0, 4, 0
    };

    // 写帧模板（大数据，value.Length>6）：[2]=9，[31]=0x80，sel/address/length 在 [51..55]，data 从 [56]
    private static readonly byte[] FrameWriteLarge = {
        2, 0, 9, 0, 50, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 0x80, 0, 0, 0, 0, 0x10, 0x0E, 0, 0,
        1, 1, 50, 0, 0, 0, 0, 0, 1, 1, 7, 8, 49, 0, 25, 0
    };

    private TcpClient _tcpClient = null!;
    private NetworkStream _stream = null!;
    private int _clientId = 1024;
    private readonly object _sync = new();
    private bool _disposed;
    private readonly RegisterMap _registerMap = new();

    /// <summary>
    /// 注册 CodePagesEncodingProvider，使 GBK / shift_jis 等非默认代码页可用。
    /// .NET 8 的共享框架内置了该 Provider（无需额外 NuGet 包）；netstandard2.0 目标没有该类型，
    /// 通过条件编译跳过。
    /// 注意：静态字段初始化器先于静态构造函数执行，因此这里必须显式注册 Provider，
    /// 否则 ResolveDefaultStringEncoding 获取 GBK 时会因未注册而抛异常。
    /// </summary>
    private static void RegisterCodePagesProvider()
    {
#if NET8_0_OR_GREATER
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        catch
        {
            // 重复注册或异常，忽略
        }
#endif
    }

    /// <summary>
    /// 字符串编码。默认 GBK(936)（中文 FANUC 控制器注释常用编码，能编码全部简体中文 + ASCII）。
    /// 注册 CodePagesEncodingProvider 后 Encoding.GetEncoding("GBK") 可用；
    /// netstandard2.0 无 provider 时回退到 Encoding.Default。调用方可按需重新赋值。
    /// 暴露为 internal 供同程序集其他管理器（如 AlarmManager）复用，避免各 Manager 各自解析 GBK 时行为不一致。
    /// </summary>
    internal static readonly Encoding DefaultStringEncoding = ResolveDefaultStringEncoding();

    private static Encoding ResolveDefaultStringEncoding()
    {
        RegisterCodePagesProvider();
#if NET8_0_OR_GREATER
        try
        {
            return Encoding.GetEncoding("GBK");
        }
        catch
        {
            // 兜底
        }
#endif
        return Encoding.Default;
    }

    private Encoding _stringEncoding = DefaultStringEncoding;

    // 断线检测（后台监视）
    private CancellationTokenSource _monitorCts = null;
    private Task _monitorTask = null;
    private const int MonitorIntervalMs = 500; // 轮询间隔

    /// <summary>
    /// 连接被对端关闭/网络异常导致断开时触发（在后台线程，订阅者需自行 marshal 回 UI 线程）。
    /// 仅当本库正持有连接（未主动 Disconnect）时才会触发，用于感知服务器主动断开。
    /// </summary>
    public event Action ConnectionLost;

    /// <summary>
    /// <summary>%R 寄存器地址中央分配器（所有 SETASG 模块共享）</summary>
    /// </summary>
    public RegisterMap RegisterMap => _registerMap;

    /// <summary>
    /// 机器人字符串编码（默认 UTF-8，无需任何 NuGet 包）。
    /// 真实 FANUC 中文注释为 GBK，调用方可按需设置（需自备 GBK 编码器）。
    /// </summary>
    public Encoding StringEncoding
    {
        get => _stringEncoding;
        set => _stringEncoding = value ?? Encoding.UTF8;
    }

    internal bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _tcpClient is { Connected: true };
            }
        }
    }

    internal void SetClientId(int clientId) => _clientId = clientId;

    /// <summary>
    /// <summary>建立 TCP 连接并完成 SNPX 握手。</summary>
    /// </summary>
    internal bool Connect(string ipAddress, int port, int timeoutMs)
    {
        Disconnect();

        var client = new TcpClient();
        var connectTask = client.ConnectAsync(ipAddress, port);
        if (!connectTask.Wait(timeoutMs))
        {
            client.Dispose();
            throw new RobotException(RobotErrorCode.ConnectionTimeout, "连接超时");
        }

        if (!client.Connected)
        {
            client.Dispose();
            throw new RobotException(RobotErrorCode.ConnectionFailed, $"无法连接到 {ipAddress}:{port}");
        }

        _tcpClient = client;
        _stream = client.GetStream();

        try
        {
            Handshake();
            StartMonitor();
            return true;
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    internal async Task<bool> ConnectAsync(string ipAddress, int port, int timeoutMs, CancellationToken ct = default)
    {
        Disconnect();

        var client = new TcpClient();
#if NET8_0_OR_GREATER
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await client.ConnectAsync(ipAddress, port, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw new RobotException(RobotErrorCode.ConnectionTimeout, "连接超时");
        }
#else
        // netstandard2.0 无 ConnectAsync(ip, port, CancellationToken) 重载，用 Task.WhenAny 实现超时
        var connectTask = client.ConnectAsync(ipAddress, port);
        var timeoutTask = Task.Delay(timeoutMs, ct);
        var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
        if (completed != connectTask || connectTask.Status != TaskStatus.RanToCompletion)
        {
            client.Dispose();
            throw new RobotException(RobotErrorCode.ConnectionTimeout, "连接超时");
        }
#endif

        if (!client.Connected)
        {
            client.Dispose();
            throw new RobotException(RobotErrorCode.ConnectionFailed, $"无法连接到 {ipAddress}:{port}");
        }

        _tcpClient = client;
        _stream = client.GetStream();

        try
        {
            await HandshakeAsync(ct).ConfigureAwait(false);
            StartMonitor();
            return true;
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    /// <summary>
    /// SNPX 握手流程：
    /// 1. 发送连接帧（全零 + [1..4]=ClientId），读响应 [0]==1
    /// 2. 发送会话帧（[0]=8），读响应 [0]==3
    /// 3. 发送 CLRASG 清空间接寻址（各 Manager 后续按需发送 SETASG 绑定）
    /// </summary>
    private void Handshake()
    {
        // 1. 连接帧：全零 + ClientId（小端写入 [1..4]）
        var connectFrame = (byte[])FrameConnect.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(connectFrame.AsSpan(1), _clientId);
        WriteAll(connectFrame);

        var response = ReadExactly(HeaderSize);
        if (response[0] != 1)
        {
            throw new RobotException(RobotErrorCode.InvalidResponse, "连接响应无效");
        }

        // 2. 会话帧：[0]=8
        WriteAll(FrameSession);
        response = ReadExactly(HeaderSize);
        if (response[0] != 3)
        {
            throw new RobotException(RobotErrorCode.InvalidResponse, "会话响应无效");
        }

        // 3. 发送 CLRASG 清空间接寻址
        SendCommandInternal("CLRASG");
    }

    private async Task HandshakeAsync(CancellationToken ct)
    {
        var connectFrame = (byte[])FrameConnect.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(connectFrame.AsSpan(1), _clientId);
        await WriteAllAsync(connectFrame, ct).ConfigureAwait(false);

        var response = await ReadExactlyAsync(HeaderSize, ct).ConfigureAwait(false);
        if (response[0] != 1)
        {
            throw new RobotException(RobotErrorCode.InvalidResponse, "连接响应无效");
        }

        await WriteAllAsync(FrameSession, ct).ConfigureAwait(false);
        response = await ReadExactlyAsync(HeaderSize, ct).ConfigureAwait(false);
        if (response[0] != 3)
        {
            throw new RobotException(RobotErrorCode.InvalidResponse, "会话响应无效");
        }

        await SendCommandInternalAsync("CLRASG", ct).ConfigureAwait(false);
    }

    internal void Disconnect()
    {
        StopMonitor();
        lock (_sync)
        {
            _stream?.Dispose();
            _stream = null!;
            _tcpClient?.Dispose();
            _tcpClient = null!;
        }
    }

    /// <summary>启动后台断线监视任务（Connect 成功后调用）。</summary>
    private void StartMonitor()
    {
        StopMonitor();
        var cts = new CancellationTokenSource();
        _monitorCts = cts;
        _monitorTask = Task.Run(() => MonitorLoopAsync(cts.Token));
    }

    /// <summary>停止后台断线监视任务。</summary>
    private void StopMonitor()
    {
        var cts = _monitorCts;
        _monitorCts = null;
        if (cts != null)
        {
            cts.Cancel();
        }
        try
        {
            _monitorTask?.Wait(TimeSpan.FromMilliseconds(200));
        }
        catch
        {
            // 监视任务异常忽略
        }
        _monitorTask = null;
    }

    /// <summary>
    /// 后台监视循环：周期性用 Socket.Poll 检测对端是否关闭。
    /// 原理：当对端正常关闭（FIN）或被 RST 时，Poll(SelectRead) 返回 true 且 Available==0；
    /// 若 Available&gt;0 说明有未读数据（服务器正常推送），不应判为断开。
    /// 检测到断开后触发 ConnectionLost 并清理连接，使 IsConnected 变 false。
    /// </summary>
    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(MonitorIntervalMs, ct).ConfigureAwait(false);

                // 快照当前 socket，避免与 Disconnect 并发时读到已被释放的对象
                Socket socket = null;
                lock (_sync)
                {
                    if (_tcpClient?.Client != null && _stream != null)
                    {
                        socket = _tcpClient.Client;
                    }
                }
                if (socket == null)
                {
                    break; // 已断开，结束监视
                }

                bool disconnected = false;
                try
                {
                    // SelectRead：返回 true 表示有数据可读或有错误/连接关闭
                    if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    {
                        disconnected = true;
                    }
                }
                catch (ObjectDisposedException)
                {
                    disconnected = true;
                }
                catch (SocketException)
                {
                    disconnected = true;
                }

                if (disconnected)
                {
                    // 先触发事件，再清理连接（事件需在后台线程触发，由订阅者 marshal）
                    ConnectionLost?.Invoke();
                    Disconnect();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch
        {
            // 其他异常忽略，避免监视任务崩溃
        }
    }

    /// <summary>
    /// <summary>构造并发送一个 SNPX 数据帧，读回响应。write=true 为写操作，false 为读操作。</summary>
    /// </summary>
    private int SnpxReadWrite(bool write, byte function, int address, int size, byte[] data, int dataSize)
    {
        if (_stream == null)
        {
            throw new RobotException(RobotErrorCode.NotConnected, "未连接");
        }

        byte[] packet;

        if (!write)
        {
            // 读帧：固定 56 字节，sel/address/length 在 [43..47]
            packet = (byte[])FrameRead.Clone();
            packet[43] = function;
            packet[44] = (byte)((address - 1) & 0xFF);
            packet[45] = (byte)(((address - 1) >> 8) & 0xFF);
            packet[46] = (byte)(size & 0xFF);
            packet[47] = (byte)((size >> 8) & 0xFF);
        }
        else
        {
            data ??= Array.Empty<byte>();
            if (data.Length > 6)
            {
                // 大数据写帧：sel/address/length 在 [51..55]，data 从 [56]，[4..5]=data 字节数
                packet = new byte[HeaderSize + data.Length];
                FrameWriteLarge.CopyTo(packet, 0);
                data.CopyTo(packet, HeaderSize);
                packet[4] = (byte)(data.Length & 0xFF);
                packet[5] = (byte)((data.Length >> 8) & 0xFF);
                packet[51] = function;
                packet[52] = (byte)((address - 1) & 0xFF);
                packet[53] = (byte)(((address - 1) >> 8) & 0xFF);
                packet[54] = (byte)(size & 0xFF);
                packet[55] = (byte)((size >> 8) & 0xFF);
            }
            else
            {
                // 小数据写帧：sel/address/length 在 [43..47]，data 从 [48]
                packet = (byte[])FrameWriteSmall.Clone();
                packet[43] = function;
                packet[44] = (byte)((address - 1) & 0xFF);
                packet[45] = (byte)(((address - 1) >> 8) & 0xFF);
                packet[46] = (byte)(size & 0xFF);
                packet[47] = (byte)((size >> 8) & 0xFF);
                data.CopyTo(packet, 48);
            }
        }

        lock (_sync)
        {
            WriteAll(packet, packet.Length);

            var response = ReadExactly(HeaderSize);

            if (write)
            {
                // 写响应：[31] 必须 == 212(0xD4)
                if (response[31] != (byte)0xD4)
                {
                    // 底层校验失败时返回 -1，上层抛 WriteError
                    throw new RobotException(RobotErrorCode.WriteError, "写入失败");
                }
                return 0;
            }

            // 读响应接受两种标志（关键兼容点）：
            //   148(0x94)：数据在 56 字节帧头之后，需再读 dataSize 字节
            //   212(0xD4)：数据内嵌在响应帧头偏移 44 处（FANUC 位信号 DI 等读返回此标志）
            // 必须兼容 212，否则真实机器人读取 DI/位信号会误判为 ReadError。
            byte flag = response[31];
            if (flag == (byte)0xD4)
            {
                // 数据内嵌在响应帧头 [44..]，直接拷贝，不再读网络
                if (data != null && dataSize > 0)
                {
                    Array.Copy(response, 44, data, 0, Math.Min(dataSize, response.Length - 44));
                }
                return dataSize;
            }

            if (flag != (byte)0x94)
            {
                throw new RobotException(RobotErrorCode.ReadError, "读取失败");
            }

            // 正常读：56 字节头之后是 dataSize 字节纯数据
            var respData = ReadExactly(dataSize);

            if (data != null && dataSize > 0)
            {
                Array.Copy(respData, 0, data, 0, Math.Min(dataSize, respData.Length));
            }

            return dataSize;
        }
    }

    /// <summary>读取 short 数组（16 位寄存器）。</summary>
    internal short[] ReadShort(byte selector, int address, int count)
    {
        var buffer = new byte[count * 2];
        int read = SnpxReadWrite(false, selector, address, count, buffer, count * 2);
        var result = new short[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2));
        }
        return result;
    }

    /// <summary>写入 short 数组。</summary>
    internal bool WriteShort(byte selector, int address, short[] values)
    {
        var buffer = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(i * 2), values[i]);
        }
        SnpxReadWrite(true, selector, address, values.Length, buffer, buffer.Length);
        return true;
    }

    /// <summary>
    /// 读取 int 数组。
    /// 语义：读取 count 个 16 位字（寄存器），返回 int[count]，每个 int 是无符号 16 位值（0~65535）。
    /// </summary>
    internal int[] ReadInt(byte selector, int address, int count)
    {
        var buffer = new byte[count * 2];
        SnpxReadWrite(false, selector, address, count, buffer, count * 2);
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            short word = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2));
            result[i] = word >= 0 ? word : word + 65536;
        }
        return result;
    }

    /// <summary>
    /// 写入 int 数组。
    /// 语义：写入 values.Length 个 16 位字，每个 int 仅取低 16 位。
    /// </summary>
    internal bool WriteInt(byte selector, int address, int[] values)
    {
        var buffer = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(i * 2), (short)(values[i] & 0xFFFF));
        }
        SnpxReadWrite(true, selector, address, values.Length, buffer, buffer.Length);
        return true;
    }

    /// <summary>
    /// 读取 bool 数组（位寄存器）。
    /// 位地址按字节对齐：address 对齐到 8 位边界，size 字段 = 对齐后的位数（8 的倍数），
    /// 读回 (对齐位数/8) 字节，再从字节流中按位提取。
    /// </summary>
    internal bool[] ReadBool(byte selector, int address, int count)
    {
        int alignedStart = address - 1 - (address - 1) % 8 + 1;
        int end = address + count - 1;
        if (end % 8 != 0)
        {
            end = end / 8 * 8 + 8;
        }
        int byteCount = (end - alignedStart + 1) / 8;
        int sizeBits = end - alignedStart + 1;  // 对齐后的位数

        var buffer = new byte[byteCount];
        SnpxReadWrite(false, selector, alignedStart, sizeBits, buffer, byteCount);

        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int bitPos = address - alignedStart + i;
            result[i] = (buffer[bitPos / 8] & (1 << (bitPos % 8))) != 0;
        }
        return result;
    }

    /// <summary>
    /// 写入 bool 数组。
    /// size 字段 = 原始位数（values.Length），数据按字节对齐打包（对齐到 8 位边界）。
    /// </summary>
    internal bool WriteBool(byte selector, int address, bool[] values)
    {
        int alignedStart = address - 1 - (address - 1) % 8 + 1;
        int end = address + values.Length - 1;
        if (end % 8 != 0)
        {
            end = end / 8 * 8 + 8;
        }
        int byteCount = (end - alignedStart + 1) / 8;

        var buffer = new byte[byteCount];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                int bitPos = address - alignedStart + i;
                buffer[bitPos / 8] |= (byte)(1 << (bitPos % 8));
            }
        }
        SnpxReadWrite(true, selector, address, values.Length, buffer, byteCount);
        return true;
    }

    /// <summary>发送命令字符串（function=56，用于 SETASG 等 KAREL 风格命令）。</summary>
    internal void SendCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return;
        }
        SendCommandInternal(command);
    }

    internal Task SendCommandAsync(string command)
    {
        SendCommand(command);
        return Task.CompletedTask;
    }

    private void SendCommandInternal(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        // function=56 写命令，address=1，size=bytes.Length
        SnpxReadWrite(true, FunctionWriteCommand, 1, bytes.Length, bytes, bytes.Length);
    }

    private async Task SendCommandInternalAsync(string command, CancellationToken ct)
    {
        // 命令发送复用同步路径（当前协议层命令发送为同步）
        await Task.Run(() => SendCommandInternal(command), ct).ConfigureAwait(false);
    }

    /// <summary>读取数值寄存器（function=8），返回 short 数组。</summary>
    internal short[] ReadRegisters(int address, int count)
    {
        var buffer = new byte[count * 2];
        SnpxReadWrite(false, FunctionReadWriteReg, address, count, buffer, count * 2);
        var result = new short[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2));
        }
        return result;
    }

    /// <summary>写入数值寄存器（function=8）。</summary>
    internal void WriteRegisters(int address, short[] values)
    {
        var buffer = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(i * 2), values[i]);
        }
        SnpxReadWrite(true, FunctionReadWriteReg, address, values.Length, buffer, buffer.Length);
    }

    private void WriteAll(byte[] buffer) => WriteAll(buffer, buffer.Length);

    private void WriteAll(byte[] buffer, int count)
    {
        _stream!.Write(buffer, 0, count);
        _stream.Flush();
    }

    private Task WriteAllAsync(byte[] buffer, CancellationToken ct)
#if NET8_0_OR_GREATER
        => _stream!.WriteAsync(buffer, 0, buffer.Length, ct);
#else
        => _stream!.WriteAsync(buffer, 0, buffer.Length);
#endif

    private byte[] ReadExactly(int count)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = _stream!.Read(buffer, offset, count - offset);
            if (read <= 0)
            {
                throw new RobotException(RobotErrorCode.ReceiveFailed, "连接响应无效");
            }
            offset += read;
        }
        return buffer;
    }

    private async Task<byte[]> ReadExactlyAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
#if NET8_0_OR_GREATER
            int read = await _stream!.ReadAsync(buffer, offset, count - offset, ct).ConfigureAwait(false);
#else
            int read = await _stream!.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
#endif
            if (read <= 0)
            {
                throw new RobotException(RobotErrorCode.ReceiveFailed, "连接响应无效");
            }
            offset += read;
        }
        return buffer;
    }

    /// <summary>释放资源。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Disconnect();
    }
}
