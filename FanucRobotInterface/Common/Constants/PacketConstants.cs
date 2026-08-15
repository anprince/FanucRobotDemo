namespace FanucRobotInterface.Common.Constants;

/// <summary>
/// SNAP 协议数据包常量定义
/// 对应官方库 LibCore.cs 中的常量定义
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// 默认端口号 (新版协议)
    /// 对应 LibCore.cs: ROBOT_PORT_NO = 60008
    /// </summary>
    public const int DefaultPort = 60008;

    /// <summary>
    /// 旧版端口号 (兼容旧版协议)
    /// 对应 LibCore.cs: ROBOT_PORT_NO_OLD = 18245
    /// </summary>
    public const int LegacyPort = 18245;

    /// <summary>
    /// 读写超时时间（毫秒）
    /// 对应 LibCore.cs: COMM_TIMEOUT = 10000
    /// </summary>
    public const int ReadWriteTimeoutMs = 10000;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// 对应 LibCore.cs: CON_TIMEOUT = 10000
    /// </summary>
    public const int ConnectionTimeoutMs = 10000;

    /// <summary>
    /// 最大缓冲区大小
    /// 对应 LibCore.cs: MAX_BUF_SIZE = 40000
    /// </summary>
    public const int MaxBufferSize = 40000;

    /// <summary>
    /// 基础消息长度（头部大小）
    /// 对应 LibCore.cs: HDR_SIZE = 56
    /// </summary>
    public const int BaseMessageLength = 56;

    /// <summary>
    /// 离散输入信号选择器
    /// 对应 LibCore.cs: SELECTOR_Q = 72
    /// 用于访问：DI, RI, UI, SI
    /// </summary>
    public const byte SelectorDiscreteInput = 72;

    /// <summary>
    /// 离散输出信号选择器
    /// 对应 LibCore.cs: SELECTOR_I = 70
    /// 用于访问：DO, RO, UO, SO
    /// </summary>
    public const byte SelectorDiscreteOutput = 70;

    /// <summary>
    /// 组输入信号选择器
    /// 对应 LibCore.cs: SELECTOR_AQ = 12
    /// 用于访问：GI, AI(需+1000偏移)
    /// </summary>
    public const byte SelectorGroupInput = 12;

    /// <summary>
    /// 组输出信号选择器
    /// 对应 LibCore.cs: SELECTOR_AI = 10
    /// 用于访问：GO, AO(需+1000偏移)
    /// </summary>
    public const byte SelectorGroupOutput = 10;

    /// <summary>
    /// 命令选择器
    /// 对应 LibCore.cs: SELECTOR_G = 56
    /// 用于发送命令到机器人控制器
    /// </summary>
    public const byte SelectorCommand = 56;

    /// <summary>
    /// 寄存器选择器
    /// 对应 LibCore.cs: SELECTOR_M = 8
    /// 用于访问寄存器
    /// </summary>
    public const byte SelectorRegister = 8;

    /// <summary>
    /// PMC（可编程控制器）宏信号选择器
    /// 对应 LibCore.cs: SELECTOR_MACRO = 76
    /// 用于访问 PMC 内部继电器
    /// </summary>
    public const byte SelectorPmc = 76;

    /// <summary>
    /// PMC K区（Keep Relay）基址偏移
    /// PMC 内部继电器在 selector=76 空间中的地址偏移
    /// 对应 SegmentOffset.PMC_K = 10000
    /// </summary>
    public const int OffsetPmcKeepRelay = 10000;

    /// <summary>
    /// PMC D区（Data Table）在 GO 空间中的基址偏移
    /// 对应 SegmentOffset.PMC_D = 10000
    /// 通过 GO selector=10 以字（unsigned 16-bit）访问
    /// </summary>
    public const int OffsetPmcDataTable = 10000;

    /// <summary>
    /// PMCR2 参数起始索引
    /// SDO Index ≥ 11001 时自动映射到 PMC 位访问（Index - 11000）
    /// 对应 Core.cs: Index &lt; 11001 → ReadSdo, else → ReadPmcr2
    /// </summary>
    public const int Pmcr2BaseIndex = 11000;

    /// <summary>
    /// Digital 信号基础偏移
    /// 用于：DI (数字输入), DO (数字输出)
    /// </summary>
    public const int AddressOffsetDigital = 0;

    /// <summary>
    /// Robot 信号偏移
    /// 用于：RI (机器人输入), RO (机器人输出)
    /// </summary>
    public const int AddressOffsetRobot = 5000;

    /// <summary>
    /// UOP (User Operation Panel) 用户操作面板信号偏移
    /// 用于：UI (用户输入), UO (用户输出)
    /// </summary>
    public const int AddressOffsetUop = 6000;

    /// <summary>
    /// SOP (Signal Operation Panel) 信号操作面板信号偏移
    /// 用于：SI (信号输入), SO (信号输出)
    /// </summary>
    public const int AddressOffsetSop = 7000;

    /// <summary>
    /// 焊接信号偏移
    /// 用于：WI (焊接输入), WO (焊接输出)
    /// 对应官方文档：ReadSDI/SDO(8000 + index)
    /// </summary>
    public const int AddressOffsetWelding = 8000;

    /// <summary>
    /// 焊接系统信号偏移
    /// 用于：WSI (焊接系统输入), WSO (焊接系统输出)
    /// 对应官方文档：ReadSDI/SDO(8400 + index)
    /// </summary>
    public const int AddressOffsetWeldingSystem = 8400;

    /// <summary>
    /// 模拟信号偏移（通过 GI/GO 寻址 AI/AO 时索引加 1000）
    /// 用于：AI (模拟输入), AO (模拟输出)
    /// 对应官方文档：ReadGI/ReadGO(1000 + index)
    /// </summary>
    public const int AddressOffsetAnalog = 1000;

    /// <summary>
    /// <summary>连接响应类型</summary>
    /// </summary>
    public const byte ResponseConnect = 1;

    /// <summary>
    /// <summary>会话响应类型</summary>
    /// </summary>
    public const byte ResponseSession = 3;

    /// <summary>
    /// <summary>短响应类型</summary>
    /// </summary>
    public const byte ResponseShort = 148;

    /// <summary>
    /// <summary>扩展响应类型</summary>
    /// </summary>
    public const byte ResponseExtended = 212;

    /// <summary>
    /// 会话请求数据包
    /// 对应 LibCore.cs: _sessionReq
    /// </summary>
    public static readonly byte[] SessionRequest =
    {
        8, 0, 1, 0, 0, 0, 0, 0, 0, 1,
        0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 192, 0, 0, 0, 0, 16, 14, 0, 0,
        1, 1, 79, 1, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0
    };

    /// <summary>
    /// 读取请求数据包
    /// 对应 LibCore.cs: _readReq
    /// </summary>
    public static readonly byte[] ReadRequest =
    {
        2, 0, 6, 0, 0, 0, 0, 0, 0, 1,
        0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        6, 192, 0, 0, 0, 0, 16, 14, 0, 0,
        1, 1, 4, 8, 0, 0, 2, 0, 0, 0,
        0, 0, 0, 0, 0, 0
    };

    /// <summary>
    /// 短写入请求数据包
    /// 对应 LibCore.cs: _sWriteReq
    /// </summary>
    public static readonly byte[] ShortWriteRequest =
    {
        2, 0, 8, 0, 0, 0, 0, 0, 0, 1,
        0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        8, 192, 0, 0, 0, 0, 16, 14, 0, 0,
        1, 1, 7, 8, 9, 0, 4, 0, 1, 0,
        2, 0, 3, 0, 4, 0
    };

    /// <summary>
    /// 长写入请求数据包
    /// 对应 LibCore.cs: _lWriteReq
    /// </summary>
    public static readonly byte[] LongWriteRequest =
    {
        2, 0, 9, 0, 50, 0, 0, 0, 0, 2,
        0, 0, 0, 0, 0, 0, 0, 2, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        9, 128, 0, 0, 0, 0, 16, 14, 0, 0,
        1, 1, 50, 0, 0, 0, 0, 0, 1, 1,
        7, 8, 49, 0, 25, 0
    };
}
