using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>字符串寄存器（SR）管理器。</summary>
/// </summary>
public class StrRegManager
{
    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<int, (int address, int words)> _cache = new();

    internal StrRegManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    /// <summary>读取单个字符串寄存器 SR[index]（同步）。</summary>
    /// <param name="index">寄存器编号（从 1 开始），如 SR[1]、SR[100]。</param>
    /// <returns>字符串内容（最多 80 字符）。</returns>
    public string Read(int index)
    {
        int address = GetOrBindAddress(index);
        // 字符串寄存器固定 40 字 = 80 字节
        int words = 40;
        var data = _client.ReadRegisters(address, words);

        // 将 short[] 转为字节（小端），按默认编码解码，截断到 null
        var bytes = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            bytes[i * 2] = (byte)(data[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((data[i] >> 8) & 0xFF);
        }

        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }

        return Encoding.Default.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>异步读取单个字符串寄存器 SR[index]。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <returns>字符串内容（最多 80 字符）。</returns>
    public Task<string> ReadAsync(int index) => Task.Run(() => Read(index));

    /// <summary>写入单个字符串寄存器 SR[index]（同步）。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <param name="value">要写入的字符串（最多 80 字符）。</param>
    public void Write(int index, string value)
    {
        int address = GetOrBindAddress(index);
        var bytes = Encoding.Default.GetBytes(value ?? string.Empty);
        int words = 40;
        var data = new short[words];
        for (int i = 0; i < words; i++)
        {
            data[i] = (short)((i * 2 < bytes.Length ? bytes[i * 2] : 0)
                             | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
        _client.WriteRegisters(address, data);
    }

    /// <summary>异步写入单个字符串寄存器 SR[index]。</summary>
    /// <param name="index">寄存器编号（从 1 开始）。</param>
    /// <param name="value">要写入的字符串（最多 80 字符）。</param>
    public Task WriteAsync(int index, string value) => Task.Run(() => Write(index, value));

    private const int WordsPerValue = 40;  // SR 每值 40 字

    /// <summary>批量读取字符串寄存器 SR[startIndex]~SR[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>字符串数组。</returns>
    public string[] ReadBatch(int startIndex, int count)
    {
        if (count <= 0) return Array.Empty<string>();

        int totalWords = count * WordsPerValue;
        if (_allocator.RemainingWords >= totalWords)
        {
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} SR[{startIndex}] 1");
            var words = _client.ReadRegisters(addr, totalWords);
            for (int i = 0; i < count; i++) _cache[startIndex + i] = (addr + i * WordsPerValue, WordsPerValue);
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                var slice = new short[WordsPerValue];
                Array.Copy(words, i * WordsPerValue, slice, 0, WordsPerValue);
                result[i] = ShortsToString(slice);
            }
            return result;
        }

        var fallback = new string[count];
        for (int i = 0; i < count; i++) fallback[i] = Read(startIndex + i);
        return fallback;
    }

    /// <summary>异步批量读取字符串寄存器 SR[startIndex]~SR[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>字符串数组。</returns>
    public Task<string[]> ReadBatchAsync(int startIndex, int count) => Task.Run(() => ReadBatch(startIndex, count));

    /// <summary>批量写入字符串寄存器 SR[startIndex]~SR[startIndex+count-1]（同步）。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">要写入的字符串数组（每个最多 80 字符）。</param>
    public void WriteBatch(int startIndex, string[] values)
    {
        if (values == null || values.Length == 0) return;

        int totalWords = values.Length * WordsPerValue;
        if (_allocator.RemainingWords >= totalWords)
        {
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} SR[{startIndex}] 1");
            for (int i = 0; i < values.Length; i++)
            {
                var bytes = Encoding.Default.GetBytes(values[i] ?? string.Empty);
                var data = new short[WordsPerValue];
                for (int w = 0; w < WordsPerValue; w++)
                {
                    data[w] = (short)((w * 2 < bytes.Length ? bytes[w * 2] : 0)
                                     | ((w * 2 + 1 < bytes.Length ? bytes[w * 2 + 1] : 0) << 8));
                }
                _client.WriteRegisters(addr + i * WordsPerValue, data);
                _cache[startIndex + i] = (addr + i * WordsPerValue, WordsPerValue);
            }
            return;
        }

        for (int i = 0; i < values.Length; i++) Write(startIndex + i, values[i]);
    }

    /// <summary>异步批量写入字符串寄存器 SR[startIndex]~SR[startIndex+count-1]。</summary>
    /// <param name="startIndex">起始寄存器编号（从 1 开始）。</param>
    /// <param name="values">要写入的字符串数组（每个最多 80 字符）。</param>
    public Task WriteBatchAsync(int startIndex, string[] values) => Task.Run(() => WriteBatch(startIndex, values));

    private int GetOrBindAddress(int index)
    {
        if (_cache.TryGetValue(index, out var entry))
        {
            return entry.address;
        }

        int address = _allocator.Allocate(40);
        string command = $"SETASG {address} 40 SR[{index}] 1";
        _client.SendCommand(command);
        _cache[index] = (address, 40);
        return address;
    }

    private static string ShortsToString(short[] data)
    {
        var bytes = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            bytes[i * 2] = (byte)(data[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((data[i] >> 8) & 0xFF);
        }

        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }

        return Encoding.Default.GetString(bytes).TrimEnd('\0');
    }
}
