namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>任务类型。</summary>
/// </summary>
public enum TaskType
{
    /// <summary>普通任务。</summary>
    Normal,

    /// <summary>忽略 KAREL 程序。</summary>
    IgnoreKarel,

    /// <summary>忽略宏程序。</summary>
    IgnoreMacro,

    /// <summary>忽略宏与 KAREL 程序。</summary>
    IgnoreMacroKarel
}
