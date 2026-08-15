using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>任务管理器。</summary>
/// </summary>
public class TaskManager
{
    private const int TaskWords = 18;  // 任务数据块固定 18 字

    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<(TaskType, int), int> _cache = new();

    internal TaskManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    /// <summary>构造任务系统变量名（PRG[1] / PRG[K1] / PRG[M1] / PRG[MK1]）。</summary>
    private static string BuildVariable(int index, TaskType type)
    {
        string prefix = type switch
        {
            TaskType.IgnoreKarel => "PRG[K",
            TaskType.IgnoreMacro => "PRG[M",
            TaskType.IgnoreMacroKarel => "PRG[MK",
            _ => "PRG["
        };
        return $"{prefix}{index}]";
    }

    /// <summary>同步读取任务状态（PRG[{prefix}{index}]）。</summary>
    /// <param name="index">任务索引（从 1 开始）。</param>
    /// <param name="type">任务监控类型（Normal=PRG[i]，IgnoreKarel=PRG[K{i}]，IgnoreMacro=PRG[M{i}]，IgnoreMacroKarel=PRG[MK{i}]）。</param>
    /// <returns>任务状态信息（程序名、行号、状态码等）。</returns>
    public TaskInfo Read(int index = 1, TaskType type = TaskType.Normal)
    {
        int address = GetOrBindAddress(index, type);
        var words = _client.ReadRegisters(address, TaskWords);
        return ParseTask(words);
    }

    /// <summary>异步读取任务状态（PRG[{prefix}{index}]）。</summary>
    /// <param name="index">任务索引（从 1 开始）。</param>
    /// <param name="type">任务监控类型（Normal=PRG[i]，IgnoreKarel=PRG[K{i}]，IgnoreMacro=PRG[M{i}]，IgnoreMacroKarel=PRG[MK{i}]）。</param>
    /// <returns>任务状态信息（程序名、行号、状态码等）。</returns>
    public Task<TaskInfo> ReadAsync(int index = 1, TaskType type = TaskType.Normal)
        => Task.Run(() => Read(index, type));

    private int GetOrBindAddress(int index, TaskType type)
    {
        var key = (type, index);
        if (_cache.TryGetValue(key, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(TaskWords);
        string command = $"SETASG {address} {TaskWords} {BuildVariable(index, type)} 1";
        _client.SendCommand(command);
        _cache[key] = address;
        return address;
    }

    /// <summary>解析任务数据（18 字）：ProgName[0..7]8字, LineNumber[8], State[9], ParentProgName[10..17]8字。</summary>
    private static TaskInfo ParseTask(short[] data)
    {
        if (data.Length < TaskWords)
        {
            throw new InvalidOperationException($"Task data too short: expected {TaskWords}");
        }

        return new TaskInfo
        {
            ProgName = ShortsToString(data, 0, 8),
            LineNumber = data[8],
            State = data[9],
            ParentProgName = ShortsToString(data, 10, 8)
        };
    }

    private static string ShortsToString(short[] data, int start, int wordCount)
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

        return Encoding.Default.GetString(bytes).TrimEnd('\0');
    }
}
