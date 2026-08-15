using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>标志（F）管理器。每个值占 1 个字（bool）。</summary>
/// </summary>
public class FlagManager
{
    private const int WordsPerValue = 1;

    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<int, int> _cache = new();

    internal FlagManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    /// <summary>读取单个标志寄存器 F[index]（同步）。</summary>
    /// <param name="index">标志寄存器编号（从 1 开始），如 F[1]、F[100]。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public bool Read(int index)
    {
        int address = GetOrBindAddress(index);
        var words = _client.ReadRegisters(address, WordsPerValue);
        return words.Length > 0 && words[0] != 0;
    }

    /// <summary>异步读取单个标志寄存器 F[index]。</summary>
    /// <param name="index">标志寄存器编号（从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public Task<bool> ReadAsync(int index) => Task.Run(() => Read(index));

    /// <summary>写入单个标志寄存器 F[index]（同步）。</summary>
    /// <param name="index">标志寄存器编号（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    public void Write(int index, bool value)
    {
        int address = GetOrBindAddress(index);
        _client.WriteRegisters(address, new[] { (short)(value ? 1 : 0) });
    }

    /// <summary>异步写入单个标志寄存器 F[index]。</summary>
    /// <param name="index">标志寄存器编号（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    public Task WriteAsync(int index, bool value) => Task.Run(() => Write(index, value));

    /// <summary>批量读取标志寄存器 F[startIndex]~F[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public bool[] ReadBatch(int startIndex, int count)
    {
        if (count <= 0) return Array.Empty<bool>();

        // 容量足够 → 批量绑定：分配 count 字，一条 SETASG {addr} {count} F[startIndex] 1，一次读
        if (_allocator.RemainingWords >= count)
        {
            int addr = _allocator.Allocate(count);
            _client.SendCommand($"SETASG {addr} {count} F[{startIndex}] 1");
            var words = _client.ReadRegisters(addr, count);
            for (int i = 0; i < count; i++) _cache[startIndex + i] = addr + i;
            var result = new bool[count];
            for (int i = 0; i < count; i++) result[i] = words.Length > i && words[i] != 0;
            return result;
        }

        // 容量不足则逐个读
        var fallback = new bool[count];
        for (int i = 0; i < count; i++) fallback[i] = Read(startIndex + i);
        return fallback;
    }

    /// <summary>异步批量读取标志寄存器 F[startIndex]~F[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public Task<bool[]> ReadBatchAsync(int startIndex, int count) => Task.Run(() => ReadBatch(startIndex, count));

    /// <summary>批量写入标志寄存器 F[startIndex]~F[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    public void WriteBatch(int startIndex, bool[] values)
    {
        if (values == null || values.Length == 0) return;

        // 容量足够 → 批量绑定 + 一次写
        if (_allocator.RemainingWords >= values.Length)
        {
            int addr = _allocator.Allocate(values.Length);
            _client.SendCommand($"SETASG {addr} {values.Length} F[{startIndex}] 1");
            var words = new short[values.Length];
            for (int i = 0; i < values.Length; i++) words[i] = values[i] ? (short)1 : (short)0;
            _client.WriteRegisters(addr, words);
            for (int i = 0; i < values.Length; i++) _cache[startIndex + i] = addr + i;
            return;
        }

        // 容量不足则逐个写
        for (int i = 0; i < values.Length; i++) Write(startIndex + i, values[i]);
    }

    /// <summary>异步批量写入标志寄存器 F[startIndex]~F[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    public Task WriteBatchAsync(int startIndex, bool[] values) => Task.Run(() => WriteBatch(startIndex, values));

    private int GetOrBindAddress(int index)
    {
        if (_cache.TryGetValue(index, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(WordsPerValue);
        string command = $"SETASG {address} {WordsPerValue} F[{index}] 1";
        _client.SendCommand(command);
        _cache[index] = address;
        return address;
    }
}
