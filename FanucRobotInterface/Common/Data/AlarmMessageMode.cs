namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>报警消息的详细程度。</summary>
/// </summary>
public enum AlarmMessageMode
{
    /// <summary>完整消息（包含原因与处理建议）。</summary>
    Full,

    /// <summary>简短消息。</summary>
    Short,

    /// <summary>中等详细程度。</summary>
    Medium
}
