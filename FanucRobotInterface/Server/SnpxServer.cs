using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FanucRobotInterface.Server.Simulation;

namespace FanucRobotInterface.Server;

/// <summary>
/// SNPX 服务器：TcpListener 监听、握手（连接帧→[0]=1、会话帧→[0]=3）、
/// 每连接独立线程处理帧循环，调用 SimulatedController 处理读/写/命令。
/// </summary>
public sealed class SnpxServer : IDisposable
{
    /// <summary>默认监听端口。</summary>
    public const int DefaultPort = 60008;

    private readonly SimulatedController _controller;
    private readonly SynchronizationContext? _uiContext;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private readonly object _sync = new();

    /// <summary>已连接客户端列表（UI 绑定）。</summary>
    public ObservableCollection<ClientInfo> Clients { get; } = new();

    /// <summary>是否正在监听。</summary>
    public bool IsListening { get; private set; }

    /// <summary>监听端口。</summary>
    public int Port { get; private set; } = DefaultPort;

    /// <summary>日志事件（UI 绑定）。</summary>
    public event Action<string>? Log;

    /// <summary>客户端连接/断开事件。</summary>
    public event Action? ClientChanged;

    public SnpxServer(SimulatedController controller, SynchronizationContext? uiContext = null)
    {
        _controller = controller;
        _uiContext = uiContext;
    }

    /// <summary>
    /// 将动作派发到 UI 线程执行。WPF 宿主传入 SynchronizationContext 时，
    /// 对 UI 绑定集合（Clients）的修改与事件触发必须回到 UI 线程，否则
    /// ObservableCollection 跨线程修改会抛 NotSupportedException。
    /// 命令行宿主传 null 时直接同步执行。
    /// </summary>
    private void PostToUi(Action action)
    {
        if (_uiContext != null)
        {
            _uiContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    /// <summary>启动监听。</summary>
    public bool Start(int port)
    {
        lock (_sync)
        {
            if (IsListening)
            {
                return true;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Port = port;
                IsListening = true;
                _cts = new CancellationTokenSource();
                _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
                Log?.Invoke($"[Server] 开始监听 0.0.0.0:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Server] 启动失败: {ex.Message}");
                IsListening = false;
                return false;
            }
        }
    }

    /// <summary>停止监听并断开所有客户端。</summary>
    public void Stop()
    {
        lock (_sync)
        {
            if (!IsListening)
            {
                return;
            }
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch
            {
                // ignore
            }

            // 断开所有客户端
            foreach (var client in Clients.ToList())
            {
                try
                {
                    client.Socket?.Close();
                }
                catch
                {
                    // ignore
                }
            }
            Clients.Clear();
            IsListening = false;
            _listener = null;
            Log?.Invoke($"[Server] 已停止监听");
            ClientChanged?.Invoke();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcpClient;
            try
            {
                // 说明：AcceptTcpClientAsync(CancellationToken) 重载在 netstandard2.0 不可用，
                // 故使用无参重载；停止时 _listener.Stop() 会抛出 ObjectDisposedException/SocketException 使循环退出。
                tcpClient = await _listener!.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            // 每个客户端一个线程（与客户端同步阻塞模型一致）
            var _ = Task.Run(() => HandleClientAsync(tcpClient, ct));
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken ct)
    {
        string remote = tcpClient.Client.RemoteEndPoint?.ToString() ?? "?";
        var info = new ClientInfo
        {
            Socket = tcpClient,
            Endpoint = remote,
            ConnectedAt = DateTime.Now
        };

        lock (_sync)
        {
            if (!IsListening)
            {
                tcpClient.Close();
                return;
            }
            // 将 Clients 集合修改派发到 UI 线程（ObservableCollection 跨线程修改会抛异常）
            PostToUi(() => Clients.Add(info));
        }
        PostToUi(() => Log?.Invoke($"[Client] + {remote} 已连接"));
        PostToUi(() => ClientChanged?.Invoke());

        try
        {
            using var stream = tcpClient.GetStream();
            bool handshaked = await DoHandshakeAsync(stream, ct).ConfigureAwait(false);
            if (!handshaked)
            {
                PostToUi(() => Log?.Invoke($"[Client] - {remote} 握手失败，断开"));
                return;
            }
            PostToUi(() => Log?.Invoke($"[Client] {remote} 握手成功"));

            // 每连接帧循环
            await FrameLoopAsync(stream, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostToUi(() => Log?.Invoke($"[Client] {remote} 异常: {ex.Message}"));
        }
        finally
        {
            try
            {
                tcpClient.Close();
            }
            catch
            {
                // ignore
            }
            lock (_sync)
            {
                PostToUi(() => Clients.Remove(info));
            }
            PostToUi(() => Log?.Invoke($"[Client] - {remote} 断开"));
            PostToUi(() => ClientChanged?.Invoke());
        }
    }

    /// <summary>
    /// SNPX 握手：收连接帧 → 回 [0]=1；收会话帧 → 回 [0]=3。
    /// </summary>
    private async Task<bool> DoHandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        // SNPX 握手是固定次序的两帧：先连接帧、再会话帧。
        // 注意：连接帧的 [1..4] 被 ClientId（小端）覆盖，[2] 并非可靠的帧类型标识，
        // 因此这里只按序读取两帧并回对应响应，不校验帧内容。
        try
        {
            // 1. 连接帧 → 回 [0]=1
            await ReadExactlyAsync(stream, SnpxFrame.HeaderSize, ct).ConfigureAwait(false);
            await WriteAllAsync(stream, BuildConnectResp(), ct).ConfigureAwait(false);

            // 2. 会话帧 → 回 [0]=3
            await ReadExactlyAsync(stream, SnpxFrame.HeaderSize, ct).ConfigureAwait(false);
            await WriteAllAsync(stream, BuildSessionResp(), ct).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>连接帧响应：[0]=1。</summary>
    private static byte[] BuildConnectResp()
    {
        var resp = new byte[SnpxFrame.HeaderSize];
        resp[0] = 1;
        return resp;
    }

    /// <summary>会话帧响应：[0]=3。</summary>
    private static byte[] BuildSessionResp()
    {
        var resp = new byte[SnpxFrame.HeaderSize];
        resp[0] = 3;
        return resp;
    }

    /// <summary>
    /// 每连接帧循环：读取 56 字节帧头，判定类型并响应。
    /// 客户端发送顺序：先 CLRASG（命令帧），随后各 Manager 发 SETASG（命令帧）与读/写帧。
    /// </summary>
    private async Task FrameLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            byte[] header;
            try
            {
                header = await ReadExactlyAsync(stream, SnpxFrame.HeaderSize, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            var parsed = SnpxFrame.ParseHeader(header);
            if (parsed == null)
            {
                return;
            }

            // 大数据区在帧头之后；小数据内联在帧头 [48..53]
            byte[]? extraData = null;
            if (parsed.Kind == FrameKind.WriteLarge)
            {
                extraData = parsed.DataByteCount > 0
                    ? await ReadExactlyAsync(stream, parsed.DataByteCount, ct).ConfigureAwait(false)
                    : Array.Empty<byte>();
            }
            else if (parsed.Kind == FrameKind.Unknown && parsed.FrameType == 8)
            {
                // 小写帧：数据内联在 [48..53]（最多 6 字节）。有效字节数依 selector 与 size 而定。
                extraData = ExtractSmallWriteData(header, parsed.Selector, parsed.Size);
            }

            switch (parsed.Kind)
            {
                case FrameKind.Read:
                {
                    // 读：回 56 帧头 [31]=0x94 + size 数据
                    byte[]? data = _controller.HandleRead(parsed.Selector, parsed.Address, parsed.Size);
                    data ??= Array.Empty<byte>();
                    var respHeader = SnpxFrame.BuildReadOkHeader();
                    await WriteAllAsync(stream, respHeader, ct).ConfigureAwait(false);
                    if (data.Length > 0)
                    {
                        await WriteAllAsync(stream, data, ct).ConfigureAwait(false);
                    }
                    break;
                }

                case FrameKind.WriteLarge:
                {
                    if (parsed.Selector == SnpxFrame.FunctionWriteCommand)
                    {
                        await HandleCommand(stream, extraData!, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        _controller.HandleWrite(parsed.Selector, parsed.Address, parsed.Size, extraData!, extraData!.Length);
                        await WriteAllAsync(stream, SnpxFrame.BuildWriteOkHeader(), ct).ConfigureAwait(false);
                    }
                    break;
                }

                default:
                {
                    // 写小帧 / 命令帧（[2]=8）
                    var kind = SnpxFrame.ClassifyWrite(header);
                    switch (kind)
                    {
                        case WriteKind.CommandSmall:
                        {
                            await HandleCommand(stream, extraData!, ct).ConfigureAwait(false);
                            break;
                        }
                        case WriteKind.Small:
                        {
                            _controller.HandleWrite(parsed.Selector, parsed.Address, parsed.Size, extraData!, extraData!.Length);
                            await WriteAllAsync(stream, SnpxFrame.BuildWriteOkHeader(), ct).ConfigureAwait(false);
                            break;
                        }
                        default:
                            return;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 提取小写帧内联数据（帧头 [48..53]，最多 6 字节）。有效长度依 selector：
    /// 命令(56)=size 字节；位写(70)=(size+7)/8；寄存器/模拟/组写=size*2 字。
    /// </summary>
    private static byte[] ExtractSmallWriteData(byte[] header, byte selector, int size)
    {
        int inlineLen = selector switch
        {
            SnpxFrame.FunctionWriteCommand => size,
            SnpxFrame.SelectorDigitalWrite => (size + 7) / 8,
            _ => size * 2
        };
        inlineLen = inlineLen < 0 ? 0 : (inlineLen > 6 ? 6 : inlineLen);
        return header.AsSpan(48, inlineLen).ToArray();
    }

    /// <summary>处理命令帧（SETASG/CLRASG/CLRALM 等），命令字符串在 data 中，回写响应。</summary>
    private async Task HandleCommand(NetworkStream stream, byte[] data, CancellationToken ct)
    {
        string command = Encoding.ASCII.GetString(data).TrimEnd('\0', ' ');
        _controller.ProcessCommand(command);
        Log?.Invoke($"[Cmd] {command}");
        await WriteAllAsync(stream, SnpxFrame.BuildWriteOkHeader(), ct).ConfigureAwait(false);
    }

    private static async Task WriteAllAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        await stream.WriteAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, ct).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new IOException("连接已关闭");
            }
            offset += read;
        }
        return buffer;
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>已连接客户端信息。</summary>
public sealed class ClientInfo
{
    public TcpClient? Socket { get; set; }
    public string Endpoint { get; set; } = "";
    public DateTime ConnectedAt { get; set; }

    /// <summary>展示用连接时长。</summary>
    public string ConnectedAtText => ConnectedAt.ToString("HH:mm:ss");
}
