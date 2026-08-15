namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>系统变量信息。</summary>
/// </summary>
public class VariableInfo
{
    /// <summary>变量名。</summary>
    public string Name { get; set; }

    /// <summary>变量类型。</summary>
    public VariableType Type { get; set; }

    /// <summary>占用字数。</summary>
    public int Size { get; set; }

    /// <summary>映射的 %R 起始地址。</summary>
    public int Address { get; set; }

    /// <summary>格式串（如 "0.0"、"1"）。</summary>
    public string Multiply { get; set; }
}
