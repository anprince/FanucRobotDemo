namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>任务信息。</summary>
/// </summary>
public class TaskInfo
{
    /// <summary>当前运行的程序名称（最多 16 字符）</summary>

    /// <summary>当前运行的程序名称（最多 16 字符）</summary>
    public string ProgName { get; set; }

    /// <summary>当前行号</summary>

    /// <summary>当前行号</summary>
    public short LineNumber { get; set; }

    /// <summary>任务状态码</summary>

    /// <summary>任务状态码</summary>
    public short State { get; set; }

    /// <summary>父程序名称（最多 16 字符），最上层调用程序为空</summary>

    /// <summary>父程序名称（最多 16 字符），最上层调用程序为空</summary>
    public string ParentProgName { get; set; }

    /// <summary>获取任务状态文本描述</summary>
    public string StateText
    {
        get
        {
            switch (State)
            {
                case 0:
                    return "运行中(Running)";
                case 1:
                    return "暂停(Pause)";
                case 2:
                    return "异常终止(Abort)";
                default:
                    return $"[{State}]";
            }
        }
    }

/// <summary>初始化实例。</summary>
    public TaskInfo()
    {
        ProgName = string.Empty;
        ParentProgName = string.Empty;
    }

    /// <summary>返回表示当前对象的字符串。</summary>
    public override string ToString()
    {
        return $"{ProgName}:{LineNumber} [{StateText}] Parent={ParentProgName}";
    }
}
