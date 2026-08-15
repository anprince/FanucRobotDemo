using System.Buffers.Binary;

namespace FanucRobotInterface.Server;

/// <summary>
/// SNPX 协议帧常量、解析与组包。
/// 与客户端 FanucRobotInterface/Common/SnpxProtocol.cs 保持字节级一致。
/// </summary>
public static class SnpxFrame
{
    /// <summary>固定帧头长度。</summary>
    public const int HeaderSize = 56;

    // ---- 帧 function / selector 常量（与客户端一致）----
    /// <summary>读/写数值寄存器 D（16 位字）。</summary>
    public const byte FunctionReadWriteReg = 8;
    /// <summary>写命令字符串（KAREL 风格，如 SETASG）。</summary>
    public const byte FunctionWriteCommand = 56;
    /// <summary>位信号读（Q 区）。</summary>
    public const byte SelectorDigitalRead = 72;
    /// <summary>位信号写（I 区）。</summary>
    public const byte SelectorDigitalWrite = 70;
    /// <summary>模拟输入 AI / 组输入 GI。</summary>
    public const byte SelectorAnalogInput = 12;
    /// <summary>模拟输出 AO / 组输出 GO。</summary>
    public const byte SelectorAnalogOutput = 10;
    /// <summary>PMC 继电器 M/R/K。</summary>
    public const byte SelectorPmcSignal = 76;
    /// <summary>PMC 数据 D。</summary>
    public const byte SelectorPmcData = 10;

    // ---- 响应校验标志 ----
    /// <summary>写操作成功响应标志（字节 [31]）。</summary>
    public const byte RespWriteOk = 212;  // 0xD4
    /// <summary>读操作成功响应标志（字节 [31]）。</summary>
    public const byte RespReadOk = 148;   // 0x94

    // ---- 位信号地址偏移（加到逻辑索引上）----
    /// <summary>DI/DO 地址偏移。</summary>
    public const int OffsetDigital = 0;
    /// <summary>RI/RO 地址偏移。</summary>
    public const int OffsetRobot = 5000;
    /// <summary>UI/UO 地址偏移。</summary>
    public const int OffsetUser = 6000;
    /// <summary>SI/SO 地址偏移。</summary>
    public const int OffsetSignal = 7000;
    /// <summary>WI/WO 地址偏移。</summary>
    public const int OffsetWeld = 8000;
    /// <summary>WSI/WSO 地址偏移。</summary>
    public const int OffsetWeldSystem = 8400;

    /// <summary>
    /// 解析收到的 56 字节帧头，判定帧类型并抽取关键字段。
    /// </summary>
    /// <returns>解析结果；若帧头长度不足或类型无法识别返回 null。</returns>
    public static ParsedFrame? ParseHeader(byte[] header)
    {
        if (header.Length < HeaderSize)
        {
            return null;
        }

        byte frameType = header[2];
        var frame = new ParsedFrame
        {
            Byte0 = header[0],
            FrameType = frameType,
            DataByteCount = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4))
        };

        switch (frameType)
        {
            case 0: // 连接帧（全零 + [1..4]=ClientId）
                frame.Kind = FrameKind.Connect;
                frame.ClientId = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
                break;

            case 1: // 会话帧（[2]=1）
                frame.Kind = FrameKind.Session;
                break;

            case 6: // 读帧：sel/address-1/length 在 [43..47]
                frame.Kind = FrameKind.Read;
                frame.Selector = header[43];
                frame.Address = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(44)) + 1;
                frame.Size = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(46));
                break;

            case 9: // 写大帧：sel/address-1/length 在 [51..55]，data 从 [56]
                frame.Kind = FrameKind.WriteLarge;
                frame.Selector = header[51];
                frame.Address = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(52)) + 1;
                frame.Size = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(54));
                break;

            default:
                // 写小帧 / 命令帧（[2]=8）：sel/address-1/size 在 [43..47]，data 内联 [48..]
                frame.Kind = FrameKind.Unknown;
                frame.Selector = header[43];
                frame.Address = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(44)) + 1;
                frame.Size = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(46));
                break;
        }

        return frame;
    }

    /// <summary>
    /// 判定一个写请求帧（function=56 命令 / 小数据写 / 大数据写）。
    /// 客户端写帧类型：小数据写 [2]=8，大数据写 [2]=9，命令写 function=56 走小/大帧。
    /// </summary>
    public static WriteKind ClassifyWrite(byte[] header)
    {
        if (header.Length < HeaderSize)
        {
            return WriteKind.Invalid;
        }
        byte frameType = header[2];
        if (frameType == 9)
        {
            return WriteKind.Large;
        }
        if (frameType == 8)
        {
            // 小数据写帧，但 function 可能为 56（命令）或 8（寄存器）或 70（位写）等
            // selector 在 [43]
            return header[43] == FunctionWriteCommand ? WriteKind.CommandSmall : WriteKind.Small;
        }
        return WriteKind.Invalid;
    }

    /// <summary>
    /// 构造一个 56 字节读响应帧头（成功），[31]=148(0x94)。
    /// </summary>
    public static byte[] BuildReadOkHeader()
    {
        var header = new byte[HeaderSize];
        header[31] = RespReadOk;
        return header;
    }

    /// <summary>
    /// 构造一个 56 字节写响应帧头（成功），[31]=212(0xD4)。
    /// </summary>
    public static byte[] BuildWriteOkHeader()
    {
        var header = new byte[HeaderSize];
        header[31] = RespWriteOk;
        return header;
    }
}

/// <summary>帧种类。</summary>
public enum FrameKind
{
    Unknown,
    Connect,
    Session,
    Read,
    WriteLarge,
}

/// <summary>写请求类型。</summary>
public enum WriteKind
{
    Invalid,
    Small,
    Large,
    CommandSmall,
    CommandLarge,
}

/// <summary>解析后的帧关键字段。</summary>
public class ParsedFrame
{
    /// <summary>帧头字节 0。</summary>
    public byte Byte0 { get; set; }

    /// <summary>帧类型（[2]）。</summary>
    public byte FrameType { get; set; }

    /// <summary>帧种类。</summary>
    public FrameKind Kind { get; set; }

    /// <summary>数据区字节数（[4..5]）。</summary>
    public ushort DataByteCount { get; set; }

    /// <summary>连接帧客户端 ID（[1..4]）。</summary>
    public int ClientId { get; set; }

    /// <summary>selector（读帧 [43]，写大帧 [51]）。</summary>
    public byte Selector { get; set; }

    /// <summary>目标地址（已 +1 还原逻辑地址）。</summary>
    public int Address { get; set; }

    /// <summary>size（字/位数量）。</summary>
    public int Size { get; set; }
}
