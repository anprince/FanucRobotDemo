using System.Collections.ObjectModel;
using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 任务状态存储。维护若干任务（PRG[i]/PRG[Ki]/PRG[Mi]/PRG[MKi]）的状态，
/// 按 18 字任务数据块填充（与 TaskManager 一致）。
/// </summary>
public sealed class TaskStore
{
    /// <summary>任务数据块固定 18 字。</summary>
    public const int TaskWords = 18;

    /// <summary>任务列表（供 UI 编辑）。变量名默认 PRG[1] 等。</summary>
    public ObservableCollection<TaskEntry> Tasks { get; } = new();

    private readonly object _sync = new();

    /// <summary>按变量名填充 18 字任务词表（未知返回全零）。</summary>
    public short[] Fill(string variableName)
    {
        lock (_sync)
        {
            var words = new short[TaskWords];
            var entry = Tasks.FirstOrDefault(t => t.Variable.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                WriteString(words, 0, 8, entry.Task.ProgName);
                words[8] = entry.Task.LineNumber;
                words[9] = entry.Task.State;
                WriteString(words, 10, 8, entry.Task.ParentProgName);
            }
            return words;
        }
    }

    /// <summary>写入 18 字任务词表回对应任务（若变量存在则更新）。</summary>
    public void Write(string variableName, short[] words)
    {
        lock (_sync)
        {
            var entry = Tasks.FirstOrDefault(t => t.Variable.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                return;
            }
            entry.Task.ProgName = ReadString(words, 0, 8);
            entry.Task.LineNumber = words.Length > 8 ? words[8] : (short)0;
            entry.Task.State = words.Length > 9 ? words[9] : (short)0;
            entry.Task.ParentProgName = ReadString(words, 10, 8);
        }
    }

    private static void WriteString(short[] words, int start, int wordCount, string? value)
    {
        var bytes = Common.SnpxProtocol.DefaultStringEncoding.GetBytes(value ?? string.Empty);
        for (int i = 0; i < wordCount; i++)
        {
            words[start + i] = (short)(
                (i * 2 < bytes.Length ? bytes[i * 2] : 0)
                | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
    }

    private static string ReadString(short[] data, int start, int wordCount)
    {
        var bytes = new byte[wordCount * 2];
        for (int i = 0; i < wordCount; i++)
        {
            bytes[i * 2] = (byte)(data[start + i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((data[start + i] >> 8) & 0xFF);
        }
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }
        return Common.SnpxProtocol.DefaultStringEncoding.GetString(bytes).TrimEnd('\0');
    }
}

/// <summary>任务条目（变量名 + 状态）。</summary>
public sealed class TaskEntry
{
    public string Variable { get; set; } = "PRG[1]";
    public TaskInfo Task { get; set; } = new();
}
