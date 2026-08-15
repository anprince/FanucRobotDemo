using System.Collections.ObjectModel;
using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 报警存储。维护报警记录列表，按客户端请求的 mode（Short/Medium/Full）填充记录词表。
/// 记录布局与 AlarmManager 一致。
/// </summary>
public sealed class AlarmStore
{
    /// <summary>当前报警记录列表（供 UI 编辑）。</summary>
    public ObservableCollection<AlarmItem> Alarms { get; } = new();

    private readonly object _sync = new();

    /// <summary>清空全部报警（CLRALM）。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            Alarms.Clear();
        }
    }

    /// <summary>添加一条报警（UI 用）。</summary>
    public void Add(AlarmItem item)
    {
        lock (_sync)
        {
            item.AlarmId = (short)(Alarms.Count + 1);
            Alarms.Add(item);
        }
    }

    /// <summary>移除一条报警（UI 用）。</summary>
    public void Remove(AlarmItem item)
    {
        lock (_sync)
        {
            Alarms.Remove(item);
        }
    }

    /// <summary>
    /// 按客户端请求填充报警词表：count 条记录，每条 recordSize 字。
    /// mode 决定记录尺寸：Short=51、Medium=91、Full=100。
    /// </summary>
    public short[] Fill(int count, AlarmMessageMode mode)
    {
        int recordSize = mode switch
        {
            AlarmMessageMode.Short => 51,
            AlarmMessageMode.Medium => 91,
            _ => 100
        };

        lock (_sync)
        {
            var words = new short[count * recordSize];
            for (int i = 0; i < count; i++)
            {
                if (i < Alarms.Count)
                {
                    FillRecord(words, i * recordSize, Alarms[i], mode, recordSize);
                }
            }
            return words;
        }
    }

    private static void FillRecord(short[] words, int offset, AlarmItem a, AlarmMessageMode mode, int recordSize)
    {
        words[offset + 0] = a.AlarmId;
        words[offset + 1] = a.AlarmNumber;
        words[offset + 2] = a.CauseAlarmId;
        words[offset + 3] = a.CauseAlarmNumber;
        words[offset + 4] = a.Severity;
        words[offset + 5] = a.Year;
        words[offset + 6] = a.Month;
        words[offset + 7] = a.Day;
        words[offset + 8] = a.Hour;
        words[offset + 9] = a.Minute;
        words[offset + 10] = a.Second;

        if (mode != AlarmMessageMode.Short && offset + 11 + 40 <= words.Length)
        {
            WriteString(words, offset + 11, 40, a.AlarmMessage);
        }
        if (mode == AlarmMessageMode.Full && offset + 51 + 40 <= words.Length)
        {
            WriteString(words, offset + 51, 40, a.CauseAlarmMessage);
        }
        if (mode == AlarmMessageMode.Full && offset + 91 + 5 <= words.Length)
        {
            WriteString(words, offset + 91, 5, a.SeverityMessage);
        }
    }

    /// <summary>按 2 字节/字小端 + NUL 写入字符串。</summary>
    private static void WriteString(short[] words, int start, int wordCount, string? value)
    {
        // 写端也使用客户端的默认编码（net8.0+ = GBK），确保读写两端编码一致。
        // 若不统一，跨平台时会因 Encoding.Default 在 .NET 5+ 返回 UTF-8 而与客户端的 GBK 不匹配，导致中文乱码。
        var bytes = Common.SnpxProtocol.DefaultStringEncoding.GetBytes(value ?? string.Empty);
        for (int i = 0; i < wordCount; i++)
        {
            words[start + i] = (short)(
                (i * 2 < bytes.Length ? bytes[i * 2] : 0)
                | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
    }
}
