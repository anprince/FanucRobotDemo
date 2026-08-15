using System.Buffers.Binary;
using FanucRobotInterface.Common.Data;

namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 模拟引擎门面。串联协议层与各存储，提供统一的读/写/命令入口。
/// 所有写操作经统一锁，保证网络写与 UI 编辑一致。
/// </summary>
public sealed class SimulatedController
{
    /// <summary>变量表 + %R 映射。</summary>
    public VariableStore Variables { get; } = new();

    /// <summary>信号内存。</summary>
    public SignalStore Signals { get; } = new();

    /// <summary>位置/系统变量词表。</summary>
    public PositionStore Positions { get; } = new();

    /// <summary>报警存储。</summary>
    public AlarmStore Alarms { get; } = new();

    /// <summary>任务存储。</summary>
    public TaskStore Tasks { get; } = new();

    /// <summary>串行一致性锁。</summary>
    private readonly object _sync = new();

    /// <summary>收到数据变更事件（供 UI 刷新）。</summary>
    public event Action<string>? DataChanged;

    /// <summary>
    /// 初始化默认信号类别与示例数据。构造后调用一次。
    /// </summary>
    public void InitializeDefaults()
    {
        lock (_sync)
        {
            // 位信号类别（baseOffset：DI/DO=0、RI/RO=5000、UI/UO=6000、SI/SO=7000、WI/WO=8000、WSI/WSO=8400）
            Signals.RegisterBitBlock(0);
            Signals.RegisterBitBlock(5000);
            Signals.RegisterBitBlock(6000);
            Signals.RegisterBitBlock(7000);
            Signals.RegisterBitBlock(8000);
            Signals.RegisterBitBlock(8400);

            // 模拟信号（AI/AO 地址=channel+1000，baseOffset=1000）；组信号（GI/GO 地址=index 无偏移，baseOffset=0）
            Signals.RegisterAnalogBlock(1000);
            Signals.RegisterGroupBlock(0);

            // PMC 信号（selector=76 位读写 R/K 区，selector=10 读写 D 区数据）
            Signals.RegisterPmcBlocks();

            // 示例数值寄存器（演示用默认值）
            var rWords = new short[20];
            WriteFloat(rWords, 0, 100.5f);
            WriteFloat(rWords, 2, -25.0f);
            WriteFloat(rWords, 4, 3.14f);
            WriteFloat(rWords, 6, 9.99f);
            Variables.TrySetVariableWords("R[1]", rWords);

            // 示例系统变量
            Variables.TrySetVariableWords("$SYSNAME", StringToShorts("SIM-ROBOT-01"));
            Variables.TrySetVariableWords("$SCR_GRP[1].$MSTERPOS", PositionStore.ToWords(MakeHomePosition()));

            // 示例注释（格式 {prefix}[C{index}]，每条 40 words）。PR[C1] 会被 IsCommentVariable 正确识别为注释，
            // 不会走 Positions 专用存储，而是作为独立词表变量。
            Variables.TrySetVariableWords("R[C1]", StringToShorts("工件计数"));
            Variables.TrySetVariableWords("R[C2]", StringToShorts("工位号"));
            Variables.TrySetVariableWords("DI[C1]", StringToShorts("启动按钮"));
            Variables.TrySetVariableWords("DI[C2]", StringToShorts("急停"));
            Variables.TrySetVariableWords("DO[C1]", StringToShorts("夹爪闭合"));
            Variables.TrySetVariableWords("SR[C1]", StringToShorts("当前程序名"));
            Variables.TrySetVariableWords("PR[C1]", StringToShorts("HOME 位姿"));
            Variables.TrySetVariableWords("F[C1]", StringToShorts("加工完成标志"));

            // 示例位置：当前位姿 POS[0] 与位置寄存器 PR[1]（客户端 ReadWorldPosition 读 POS[0]，PosReg 读 PR[i]）
            Positions.Write("POS[0]", PositionStore.ToWords(MakeHomePosition()));
            Positions.Write("POS[1]", PositionStore.ToWords(MakeHomePosition()));
            Positions.Write("PR[1]", PositionStore.ToWords(MakeHomePosition()));

            // 示例任务
            Tasks.Tasks.Add(new TaskEntry { Variable = "PRG[1]", Task = new TaskInfo { ProgName = "MAIN", LineNumber = 12, State = 0 } });

            // 示例报警
            Alarms.Add(new AlarmItem
            {
                AlarmNumber = 1001,
                Severity = 1,
                Year = 26, Month = 8, Day = 14, Hour = 10, Minute = 30, Second = 0,
                AlarmMessage = "Servo error",
                CauseAlarmMessage = "Overload",
                SeverityMessage = "ERR"
            });
            Alarms.Add(new AlarmItem
            {
                AlarmNumber = 2003,
                Severity = 2,
                Year = 26, Month = 8, Day = 14, Hour = 11, Minute = 5, Second = 15,
                AlarmMessage = "Interlock",
                CauseAlarmMessage = "",
                SeverityMessage = "WARN"
            });
        }
    }

    private static PositionInfo MakeHomePosition()
    {
        var p = new PositionInfo { ValidCartesian = 1, ValidJoint = 1, UF = 1, UT = 1 };
        p.Cartesian.X = 500; p.Cartesian.Y = 0; p.Cartesian.Z = 1000;
        p.Cartesian.W = 180; p.Cartesian.P = 0; p.Cartesian.R = 0;
        p.Joint.J1 = 0; p.Joint.J2 = -30; p.Joint.J3 = 60;
        p.Joint.J4 = 0; p.Joint.J5 = 0; p.Joint.J6 = 0;
        return p;
    }

    // ---- 命令处理 ----

    /// <summary>处理命令字符串（SETASG/CLRASG/CLRALM 等），返回是否需要回写响应。成功返回 true。</summary>
    public bool ProcessCommand(string command)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return true;
            }
            string trimmed = command.Trim('\0').Trim();
            if (trimmed.Equals("CLRALM", StringComparison.OrdinalIgnoreCase))
            {
                Alarms.Clear();
                DataChanged?.Invoke("alarm");
                return true;
            }
            if (trimmed.Equals("CLRASG", StringComparison.OrdinalIgnoreCase))
            {
                Variables.Clear();
                DataChanged?.Invoke("binding");
                return true;
            }
            bool ok = Variables.ProcessCommand(trimmed);
            if (ok)
            {
                DataChanged?.Invoke("binding");
            }
            return ok;
        }
    }

    // ---- 读操作 ----

    /// <summary>
    /// 处理读请求，返回要回给客户端的数据字节（不含 56 字节帧头）。
    /// 无法识别时返回 null。
    /// </summary>
    public byte[]? HandleRead(byte selector, int address, int size)
    {
        lock (_sync)
        {
            switch (selector)
            {
                case 70: // 位信号读，输出区 Q（客户端 DigitalSignal.DO/RO/UO/SO/WO/WSO 用 SelectorDigitalWrite=70 读取）
                case 72: // 位信号读，输入区 I（客户端 DigitalSignal.DI/RI/UI/SI/WI/WSI 用 SelectorDigitalRead=72 读取）
                {
                    // 真实控制器 DI(输入区 I) 与 DO(输出区 Q) 相互独立，通过 selector 区分读写方向
                    bool isInput = selector == 72;
                    // 按 8 位对齐：起始地址对齐、size 向上取整到 8 的倍数，回传 size/8 字节
                    int alignedStart = address - 1 - (address - 1) % 8 + 1;
                    int end = address + size - 1;
                    if (end % 8 != 0)
                    {
                        end = end / 8 * 8 + 8;
                    }
                    int bitCount = end - alignedStart + 1;
                    var bits = Signals.ReadBits(alignedStart, bitCount, isInput);
                    var bytes = new byte[bitCount / 8];
                    for (int i = 0; i < bitCount; i++)
                    {
                        if (bits[i])
                        {
                            bytes[i / 8] |= (byte)(1 << (i % 8));
                        }
                    }
                    DataChanged?.Invoke("signal");
                    return bytes;
                }

                case 12: // 输入区：组输入 GI（address<1000，每通道 1 字）/ 模拟输入 AI（address=channel+1000，每通道 2 字）
                {
                    if (address >= 1000)
                    {
                        // 模拟输入 AI：每通道 2 字（32 位），size 为通道数
                        var ints = Signals.ReadAnalog(address, size, isInput: true);
                        var bytes = new byte[size * 2];
                        for (int i = 0; i < size; i++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)(ints[i] & 0xFFFF));
                        }
                        DataChanged?.Invoke("signal");
                        return bytes;
                    }
                    else
                    {
                        // 组输入 GI：每通道 1 字（16 位），size 为通道数
                        var groups = Signals.ReadGroup(address, size, isInput: true);
                        var bytes = new byte[size * 2];
                        for (int i = 0; i < size; i++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)groups[i]);
                        }
                        DataChanged?.Invoke("signal");
                        return bytes;
                    }
                }

                case 10: // 输出区：组输出 GO（address<1000）/ 模拟输出 AO（1000<=address<10000）/ PMC D 数据（address>=10000）
                {
                    if (address >= 10000)
                    {
                        // PMC D 区数据：每 1 字（16 位）
                        var datas = Signals.ReadPmcData(address, size);
                        var bytes = new byte[size * 2];
                        for (int i = 0; i < size; i++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)(datas[i] & 0xFFFF));
                        }
                        DataChanged?.Invoke("signal");
                        return bytes;
                    }
                    else if (address >= 1000)
                    {
                        // 模拟输出 AO：每通道 2 字（32 位）
                        var ints = Signals.ReadAnalog(address, size, isInput: false);
                        var bytes = new byte[size * 2];
                        for (int i = 0; i < size; i++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)(ints[i] & 0xFFFF));
                        }
                        DataChanged?.Invoke("signal");
                        return bytes;
                    }
                    else
                    {
                        // 组输出 GO：每通道 1 字（16 位）
                        var groups = Signals.ReadGroup(address, size, isInput: false);
                        var bytes = new byte[size * 2];
                        for (int i = 0; i < size; i++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)groups[i]);
                        }
                        DataChanged?.Invoke("signal");
                        return bytes;
                    }
                }

                case 76: // PMC 位信号（R 区 address=index，K 区 address=10000+index），selector=76
                {
                    // 按 8 位对齐打包（与位信号读一致）
                    int alignedStart = address - 1 - (address - 1) % 8 + 1;
                    int end = address + size - 1;
                    if (end % 8 != 0)
                    {
                        end = end / 8 * 8 + 8;
                    }
                    int bitCount = end - alignedStart + 1;
                    var bits = Signals.ReadPmcBits(alignedStart, bitCount);
                    var bytes = new byte[bitCount / 8];
                    for (int i = 0; i < bitCount; i++)
                    {
                        if (bits[i])
                        {
                            bytes[i / 8] |= (byte)(1 << (i % 8));
                        }
                    }
                    DataChanged?.Invoke("signal");
                    return bytes;
                }

                default: // function=8 寄存器读（R/SR/F/POS/$VAR/ALM/PRG 等，走 %R）
                {
                    var words = ReadRegisterWords(address, size);
                    var bytes = new byte[size * 2];
                    for (int i = 0; i < size; i++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), words[i]);
                    }
                    DataChanged?.Invoke("variable");
                    return bytes;
                }
            }
        }
    }

    // ---- 写操作 ----

    /// <summary>
    /// 处理写请求（函数 8 寄存器写 / 70 位写 / 10 AO·GO 写）。
    /// data 为帧携带的字节（dataSize 字节）。
    /// </summary>
    public void HandleWrite(byte selector, int address, int size, byte[] data, int dataSize)
    {
        lock (_sync)
        {
            switch (selector)
            {
                case 70: // 位信号写（写输出区 Q，即 DO/RO/UO/SO/WO/WSO）
                {
                    // 客户端 WriteBool 按 8 位对齐打包：数据字节的 bit j 对应地址 alignedStart + j，
                    // 而帧中的 address 字段是原始起始地址（非对齐值）。需按相同对齐规则反解。
                    int alignedStart = address - 1 - (address - 1) % 8 + 1;
                    var bits = new bool[size];
                    for (int i = 0; i < size; i++)
                    {
                        int bitPos = (address + i) - alignedStart;
                        bits[i] = bitPos >= 0 && (bitPos / 8) < dataSize
                            ? (data[bitPos / 8] & (1 << (bitPos % 8))) != 0
                            : false;
                    }
                    Signals.WriteBits(address, bits, isInput: false); // 写输出区 Q（DO）
                    DataChanged?.Invoke("signal");
                    break;
                }

                case 10: // 输出区写：组输出 GO（address<1000）/ 模拟输出 AO（1000<=address<10000）/ PMC D 数据（address>=10000）
                {
                    int words = dataSize / 2;
                    var ints = new int[words];
                    for (int i = 0; i < words; i++)
                    {
                        ints[i] = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2)) & 0xFFFF;
                    }
                    if (address >= 10000)
                    {
                        Signals.WritePmcData(address, ints);
                    }
                    else if (address >= 1000)
                    {
                        Signals.WriteAnalog(address, ints, isInput: false);
                    }
                    else
                    {
                        Signals.WriteGroup(address, ints.Select(v => (ushort)v).ToArray(), isInput: false);
                    }
                    DataChanged?.Invoke("signal");
                    break;
                }

                case 76: // PMC 位信号写（R 区 address=index，K 区 address=10000+index），selector=76
                {
                    // 与位信号写一致：按 8 位对齐反解
                    int alignedStart = address - 1 - (address - 1) % 8 + 1;
                    var bits = new bool[size];
                    for (int i = 0; i < size; i++)
                    {
                        int bitPos = (address + i) - alignedStart;
                        bits[i] = bitPos >= 0 && (bitPos / 8) < dataSize
                            ? (data[bitPos / 8] & (1 << (bitPos % 8))) != 0
                            : false;
                    }
                    Signals.WritePmcBits(address, bits);
                    DataChanged?.Invoke("signal");
                    break;
                }

                default: // function=8 寄存器写
                {
                    int words = dataSize / 2;
                    var shorts = new short[words];
                    for (int i = 0; i < words; i++)
                    {
                        shorts[i] = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2));
                    }
                    WriteRegisterWords(address, shorts);
                    DataChanged?.Invoke("variable");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 读取寄存器字词表，特殊变量（ALM 报警、PRG 任务、POS/PR 位置）从专用存储填充。
    /// </summary>
    private short[] ReadRegisterWords(int address, int size)
    {
        // 若该区间绑定到专用存储（报警/任务/位置），从对应存储按偏移填充
        if (Variables.TryGetBindingInfo(address, out int bindStart, out string name))
        {
            int offset = address - bindStart;
            if (IsPositionVariable(name))
            {
                var posWords = Positions.Read(name);
                var words = new short[size];
                for (int i = 0; i < size && (offset + i) < posWords.Length; i++)
                {
                    words[i] = posWords[offset + i];
                }
                return words;
            }
            if (IsTaskVariable(name))
            {
                var taskWords = Tasks.Fill(name);
                var words = new short[size];
                for (int i = 0; i < size && (offset + i) < taskWords.Length; i++)
                {
                    words[i] = taskWords[offset + i];
                }
                return words;
            }
        }

        var vars = Variables.Read(address, size);
        if (Variables.TryGetBindingName(address, out string alarmName) && IsAlarmVariable(alarmName))
        {
            return Alarms.Fill(size / GetAlarmRecordSize(alarmName), ResolveAlarmMode(alarmName));
        }
        return vars;
    }

    /// <summary>
    /// 写入寄存器字词表，特殊变量落到专用存储。
    /// </summary>
    private void WriteRegisterWords(int address, short[] shorts)
    {
        // 专用存储（位置）：需按 %R 偏移合并写入对应块，而非从块头覆盖
        // （客户端 WriteJointData 只写关节部分，位于块内字偏移 26 起）
        if (Variables.TryGetBindingInfo(address, out int bindStart, out string name)
            && IsPositionVariable(name))
        {
            int offset = address - bindStart;
            var block = Positions.Read(name);
            for (int i = 0; i < shorts.Length && (offset + i) < block.Length; i++)
            {
                block[offset + i] = shorts[i];
            }
            Positions.Write(name, block);
            return;
        }

        // 任务变量：客户端一次性写满 18 字块，走 TaskStore 全量写入
        if (Variables.TryGetBindingName(address, out string taskName) && IsTaskVariable(taskName))
        {
            Tasks.Write(taskName, shorts);
            return;
        }

        Variables.Write(address, shorts);
    }

    private static bool IsAlarmVariable(string name) => name.StartsWith("ALM[", StringComparison.Ordinal);
    private static bool IsTaskVariable(string name) => name.StartsWith("PRG[", StringComparison.Ordinal);

    /// <summary>
    /// 判断是否为注释变量（格式 {prefix}[C{index}]，如 R[C1]、DI[C5]、PR[C1]）。
    /// 必须在位置/任务/报警判断之前调用，避免 PR[C1] 被 IsPositionVariable 误判为位置寄存器。
    /// </summary>
    private static bool IsCommentVariable(string name)
    {
        int open = name.IndexOf('[');
        int close = name.IndexOf(']');
        if (open <= 0 || close < open + 2)
        {
            return false;
        }
        string inner = name.Substring(open + 1, close - open - 1);
        return inner.Length > 1 && inner[0] == 'C' && int.TryParse(inner.Substring(1), out _);
    }

    private static bool IsPositionVariable(string name) =>
        !IsCommentVariable(name)
        && (name.StartsWith("POS[", StringComparison.Ordinal) || name.StartsWith("PR[", StringComparison.Ordinal));

    /// <summary>从报警变量名解析 recordSize（ALM[E1]@1.100 → 100；Full 模式无 @ 后缀 → 100）。</summary>
    private static int GetAlarmRecordSize(string name)
    {
        int at = name.IndexOf('@');
        if (at >= 0 && at + 1 < name.Length)
        {
            var sizePart = name.Substring(at + 1);
            int dot = sizePart.IndexOf('.');
            if (dot >= 0 && dot + 1 < sizePart.Length)
            {
                var last = sizePart.Substring(dot + 1);
                if (int.TryParse(last, out int rs))
                {
                    return rs;
                }
            }
        }
        return 100; // Full 默认 100
    }

    private static AlarmMessageMode ResolveAlarmMode(string name)
    {
        int size = GetAlarmRecordSize(name);
        return size switch
        {
            51 => AlarmMessageMode.Short,
            91 => AlarmMessageMode.Medium,
            _ => AlarmMessageMode.Full
        };
    }

    /// <summary>读取位信号（供 UI）。isInput=true 读输入区（DI），false 读输出区（DO）。</summary>
    public bool GetBit(int baseOffset, int index, bool isInput) => Signals.GetBitByOffset(baseOffset, index, isInput);

    /// <summary>设置位信号（供 UI）。isInput=true 写输入区（DI），false 写输出区（DO）。</summary>
    public void SetBit(int baseOffset, int index, bool value, bool isInput)
    {
        lock (_sync)
        {
            Signals.SetBitByOffset(baseOffset, index, value, isInput);
            DataChanged?.Invoke("signal");
        }
    }

    /// <summary>读取组信号（供 UI）。isInput=true 读 GI 输入区，false 读 GO 输出区。</summary>
    public ushort GetGroup(int baseOffset, int index, bool isInput) => Signals.GetGroupByOffset(baseOffset, index, isInput);

    /// <summary>设置组信号（供 UI）。isInput=true 写 GI 输入区，false 写 GO 输出区。</summary>
    public void SetGroup(int baseOffset, int index, ushort value, bool isInput)
    {
        lock (_sync)
        {
            Signals.SetGroupByOffset(baseOffset, index, value, isInput);
            DataChanged?.Invoke("signal");
        }
    }

    /// <summary>读取模拟信号（供 UI）。isInput=true 读 AI 输入区，false 读 AO 输出区。</summary>
    public int GetAnalog(int baseOffset, int index, bool isInput) => Signals.GetAnalogByOffset(baseOffset, index, isInput);

    /// <summary>设置模拟信号（供 UI）。isInput=true 写 AI 输入区，false 写 AO 输出区。</summary>
    public void SetAnalog(int baseOffset, int index, int value, bool isInput)
    {
        lock (_sync)
        {
            Signals.SetAnalogByOffset(baseOffset, index, value, isInput);
            DataChanged?.Invoke("signal");
        }
    }

    /// <summary>读取 PMC 位信号（供 UI）。zone=0 表示 R 区，zone=1 表示 K 区。</summary>
    public bool GetPmcBit(int zone, int index) => Signals.GetPmcBitByOffset(zone, index);

    /// <summary>设置 PMC 位信号（供 UI）。zone=0 表示 R 区，zone=1 表示 K 区。</summary>
    public void SetPmcBit(int zone, int index, bool value)
    {
        lock (_sync)
        {
            Signals.SetPmcBitByOffset(zone, index, value);
            DataChanged?.Invoke("signal");
        }
    }

    /// <summary>读取 PMC D 区数据（供 UI）。</summary>
    public int GetPmcData(int index) => Signals.GetPmcDataByOffset(index);

    /// <summary>设置 PMC D 区数据（供 UI）。</summary>
    public void SetPmcData(int index, int value)
    {
        lock (_sync)
        {
            Signals.SetPmcDataByOffset(index, value);
            DataChanged?.Invoke("signal");
        }
    }

    private static short[] StringToShorts(string value)
    {
        var bytes = Common.SnpxProtocol.DefaultStringEncoding.GetBytes(value ?? string.Empty);
        var words = new short[40];
        for (int i = 0; i < 40; i++)
        {
            words[i] = (short)((i * 2 < bytes.Length ? bytes[i * 2] : 0)
                             | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
        return words;
    }

    private static void WriteFloat(short[] words, int index, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        words[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0));
        words[index + 1] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
    }
}
