using System;

namespace FanucRobotInterface.Common.Exceptions;

/// <summary>
/// FANUC 机器人通信/操作异常。
/// 当 SNPX 协议通信失败、指令执行出错或机器人返回错误状态时引发。
/// </summary>
public class RobotException : Exception
{
    /// <summary>获取与异常关联的错误码。</summary>
    public RobotErrorCode ErrorCode { get; }

    /// <summary>使用错误消息创建异常。</summary>
    /// <param name="errorCode">错误码。</param>
    /// <param name="message">描述错误的详细信息。</param>
    public RobotException(RobotErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>使用错误消息和内部异常创建异常。</summary>
    /// <param name="errorCode">错误码。</param>
    /// <param name="message">描述错误的详细信息。</param>
    /// <param name="innerException">导致当前异常的內部异常。</param>
    public RobotException(RobotErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>返回表示当前异常的字符串。</summary>
    /// <returns>格式为 [ErrorCode] Message 的字符串。</returns>
    public override string ToString()
    {
        return $"[{ErrorCode}] {Message}";
    }
}
