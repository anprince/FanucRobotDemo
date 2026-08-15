namespace FanucRobotInterface.Common.Exceptions;

/// <summary>
/// FANUC 机器人操作错误码。
/// 用于标识通信、协议、读写或参数验证失败的类别。
/// </summary>
public enum RobotErrorCode
{
    /// <summary>成功。</summary>
    Success = 0,
    /// <summary>未连接。</summary>
    NotConnected = 1001,
    /// <summary>连接失败。</summary>
    ConnectionFailed = 1002,
    /// <summary>连接超时。</summary>
    ConnectionTimeout = 1003,
    /// <summary>连接被拒绝。</summary>
    ConnectionRefused = 1004,
    /// <summary>协议错误。</summary>
    ProtocolError = 2001,
    /// <summary>响应无效。</summary>
    InvalidResponse = 2002,
    /// <summary>发送失败。</summary>
    SendFailed = 2003,
    /// <summary>接收失败。</summary>
    ReceiveFailed = 2004,
    /// <summary>读取错误。</summary>
    ReadError = 3001,
    /// <summary>写入错误。</summary>
    WriteError = 3002,
    /// <summary>地址无效。</summary>
    InvalidAddress = 3003,
    /// <summary>数据无效。</summary>
    InvalidData = 3004,
    /// <summary>参数为空。</summary>
    ArgumentNull = 4001,
    /// <summary>参数超出范围。</summary>
    ArgumentOutOfRange = 4002
}
