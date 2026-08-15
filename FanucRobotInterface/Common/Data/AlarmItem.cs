using System;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// 报警信息
/// </summary>
public class AlarmItem
{
    /// <summary>报警 ID（同一位置多次报警的累计编号）</summary>
    public short AlarmId { get; set; }

    /// <summary>报警编号（机器人控制器定义的报警代码）</summary>
    public short AlarmNumber { get; set; }

    /// <summary>原因报警 ID（引起本报警的源头报警 ID，0=无）</summary>
    public short CauseAlarmId { get; set; }

    /// <summary>原因报警编号（引起本报警的源头报警编号）</summary>
    public short CauseAlarmNumber { get; set; }

    /// <summary>
    /// 严重级别（由机器人控制器定义的原始严重程度代码，含义因报警类型而异）
    /// ⚠ 官方未公开完整映射表，建议对照机器人示教器界面解读
    /// </summary>
    public short Severity { get; set; }

    /// <summary>报警发生年份</summary>
    public short Year { get; set; }

    /// <summary>报警发生月份（1-12）</summary>
    public short Month { get; set; }

    /// <summary>报警发生日期（1-31）</summary>
    public short Day { get; set; }

    /// <summary>报警发生时（0-23）</summary>
    public short Hour { get; set; }

    /// <summary>报警发生分（0-59）</summary>
    public short Minute { get; set; }

    /// <summary>报警发生秒（0-59）</summary>
    public short Second { get; set; }

    /// <summary>报警消息文本（最多 40 字符，Short 模式下为空）</summary>
    public string AlarmMessage { get; set; }

    /// <summary>原因报警消息文本（最多 40 字符，仅 Medium/Full 模式）</summary>
    public string CauseAlarmMessage { get; set; }

    /// <summary>严重级别文本描述（最多 10 字符，仅 Full 模式）</summary>
    public string SeverityMessage { get; set; }

    /// <summary>报警时间（合并 Year/Month/Day/Hour/Minute/Second），可能为空</summary>
    public DateTime? Timestamp
    {
        get
        {
            try
            {
                return new DateTime(2000 + Year, Month, Day, Hour, Minute, Second);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>初始化实例。</summary>
    public AlarmItem()
    {
        AlarmMessage = string.Empty;
        CauseAlarmMessage = string.Empty;
        SeverityMessage = string.Empty;
    }

    /// <summary>返回表示当前对象的字符串。</summary>
    public override string ToString()
    {
        return $"#{AlarmNumber} [Severity={Severity}] {AlarmMessage} ({Year + 2000}-{Month}-{Day} {Hour}:{Minute}:{Second})";
    }
}
