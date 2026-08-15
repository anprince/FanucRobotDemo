using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>数值寄存器（R）管理器。每个值占 2 个字（float）。</summary>
/// </summary>
public class NumRegManager
{
    private const int WordsPerValue = 2;

    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<int, int> _cache = new();

    internal NumRegManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    // ---- 单个 ----

    /// <summary>读取单个数值寄存器 R[index]（同步）。</summary>
    /// <param name="index">寄存器编号（从 1 开始），如 R[1]、R[100]。</param>
    /// <returns>32 位浮点数值。</returns>
    public float Read(int index)
    {
        int address = GetOrBindAddress(index);
        var words = _client.ReadRegisters(address, WordsPerValue);
        return ShortsToFloat(words);
    }

    /// <summary>异步读取单个数值寄存器 R[index]。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <returns>32 位浮点数值。</returns>
    public Task<float> ReadAsync(int index) => Task.Run(() => Read(index));

    /// <summary>写入单个数值寄存器 R[index]（同步）。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <param name="value">要写入的 32 位浮点数值。</param>
    public void Write(int index, float value)
    {
        int address = GetOrBindAddress(index);
        var words = FloatToShorts(value);
        _client.WriteRegisters(address, words);
    }

    /// <summary>异步写入单个数值寄存器 R[index]。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <param name="value">要写入的 32 位浮点数值。</param>
    public Task WriteAsync(int index, float value) => Task.Run(() => Write(index, value));

    // ---- 批量 ----

    /// <summary>批量读取数值寄存器 R[startIndex]~R[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>32 位浮点数值数组。</returns>
    public float[] ReadBatch(int startIndex, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<float>();
        }

        int totalWords = count * WordsPerValue;
        if (_allocator.RemainingWords >= totalWords)
        {
            // 分配连续块，发一条 SETASG {addr} {totalWords} R[startIndex] 0，一次读
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} R[{startIndex}] 0");
            var words = _client.ReadRegisters(addr, totalWords);
            for (int i = 0; i < count; i++)
            {
                _cache[startIndex + i] = addr + i * WordsPerValue;
            }
            return WordsToFloats(words);
        }

        // 容量不足则逐个读
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = Read(startIndex + i);
        }
        return result;
    }

    /// <summary>异步批量读取数值寄存器 R[startIndex]~R[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>32 位浮点数值数组。</returns>
    public Task<float[]> ReadBatchAsync(int startIndex, int count) => Task.Run(() => ReadBatch(startIndex, count));

    /// <summary>批量写入数值寄存器 R[startIndex]~R[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">要写入的 32 位浮点数值数组。</param>
    public void WriteBatch(int startIndex, float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return;
        }

        int totalWords = values.Length * WordsPerValue;
        if (_allocator.RemainingWords >= totalWords)
        {
            // 分配连续块，发一条 SETASG {addr} {totalWords} R[startIndex] 0，一次写
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} R[{startIndex}] 0");
            var words = FloatsToWords(values);
            _client.WriteRegisters(addr, words);
            for (int i = 0; i < values.Length; i++)
            {
                _cache[startIndex + i] = addr + i * WordsPerValue;
            }
            return;
        }

        // 容量不足则逐个写
        for (int i = 0; i < values.Length; i++)
        {
            Write(startIndex + i, values[i]);
        }
    }

    /// <summary>异步批量写入数值寄存器 R[startIndex]~R[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">要写入的 32 位浮点数值数组。</param>
    public Task WriteBatchAsync(int startIndex, float[] values) => Task.Run(() => WriteBatch(startIndex, values));

    // ---- 内部 ----

    private int GetOrBindAddress(int index)
    {
        if (_cache.TryGetValue(index, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(WordsPerValue);

        // 发送 SETASG 命令把逻辑寄存器号 index 绑定到 scratch 地址（R 为 float，fmt=0）
        string command = $"SETASG {address} {WordsPerValue} R[{index}] 0";
        _client.SendCommand(command);

        _cache[index] = address;
        return address;
    }

    private static float ShortsToFloat(short[] words)
    {
        if (words.Length < WordsPerValue)
        {
            return 0f;
        }
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), words[0]);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), words[1]);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static short[] FloatToShorts(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new[]
        {
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0)),
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2))
        };
    }

    private static float[] WordsToFloats(short[] words)
    {
        var result = new float[words.Length / WordsPerValue];
        for (int i = 0; i < result.Length; i++)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), words[i * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), words[i * 2 + 1]);
            result[i] = BitConverter.ToSingle(bytes, 0);
        }
        return result;
    }

    private static short[] FloatsToWords(float[] values)
    {
        var words = new short[values.Length * WordsPerValue];
        for (int i = 0; i < values.Length; i++)
        {
            var bytes = BitConverter.GetBytes(values[i]);
            words[i * 2] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0));
            words[i * 2 + 1] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
        }
        return words;
    }
}
