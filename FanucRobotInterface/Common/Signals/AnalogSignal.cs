using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// 模拟（32 位）信号。每个通道占 2 个字（16 位），地址 = channel + 1000。
/// AI selector=12，AO selector=10。
/// </summary>
public class AnalogSignal : SignalBase<int>
{
    private readonly SnpxProtocol _client;
    private readonly byte _selector;

    internal AnalogSignal(SnpxProtocol client, SignalCategory category)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));

        Category = category;
        // 原 DLL：AI = 12，AO = 10
        _selector = category == SignalCategory.AI
            ? SnpxProtocol.SelectorAnalogInput    // AI = 12
            : SnpxProtocol.SelectorAnalogOutput;  // AO = 10
    }

    /// <summary>读取单个模拟信号值（同步）。</summary>
    /// <param name="channel">模拟通道号（从 1 开始），如 AI[1]、AO[1]。</param>
    /// <returns>模拟信号值（32 位有符号整数）。</returns>
    public override int ReadSingle(int channel)
    {
        // 每通道 2 字（32 位小端）
        var words = _client.ReadInt(_selector, channel + 1000, 2);
        if (words.Length < 2)
        {
            return 0;
        }
        return (words[1] << 16) | words[0];
    }

    /// <summary>批量读取模拟信号值（同步）。</summary>
    /// <param name="startChannel">起始通道号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>模拟信号值数组（32 位有符号整数）。</returns>
    public override int[] Read(int startChannel, int count)
    {
        // 读 count*2 字，每 2 字拼一个 32 位值
        var words = _client.ReadInt(_selector, startChannel + 1000, count * 2);
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = (words[i * 2 + 1] << 16) | words[i * 2];
        }
        return result;
    }

    /// <summary>写入单个模拟信号值（同步）。</summary>
    /// <param name="channel">模拟通道号（从 1 开始）。</param>
    /// <param name="value">要写入的值（32 位有符号整数）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool WriteSingle(int channel, int value)
    {
        var words = new[]
        {
            value & 0xFFFF,
            (value >> 16) & 0xFFFF
        };
        return _client.WriteInt(_selector, channel + 1000, words);
    }

    /// <summary>批量写入模拟信号值（同步）。</summary>
    /// <param name="startChannel">起始通道号（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（32 位有符号整数）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool Write(int startChannel, int[] values)
    {
        var words = new int[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            words[i * 2] = values[i] & 0xFFFF;
            words[i * 2 + 1] = (values[i] >> 16) & 0xFFFF;
        }
        return _client.WriteInt(_selector, startChannel + 1000, words);
    }

    /// <summary>异步读取单个模拟信号值。</summary>
    /// <param name="channel">模拟通道号（从 1 开始）。</param>
    /// <returns>模拟信号值（32 位有符号整数）。</returns>
    public override Task<int> ReadSingleAsync(int channel)
    {
        return Task.Run(() => ReadSingle(channel));
    }

    /// <summary>异步批量读取模拟信号值。</summary>
    /// <param name="startChannel">起始通道号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>模拟信号值数组（32 位有符号整数）。</returns>
    public override Task<int[]> ReadAsync(int startChannel, int count)
    {
        return Task.Run(() => Read(startChannel, count));
    }

    /// <summary>异步写入单个模拟信号值。</summary>
    /// <param name="channel">模拟通道号（从 1 开始）。</param>
    /// <param name="value">要写入的值（32 位有符号整数）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteSingleAsync(int channel, int value)
    {
        return Task.Run(() => WriteSingle(channel, value));
    }

    /// <summary>异步批量写入模拟信号值。</summary>
    /// <param name="startChannel">起始通道号（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（32 位有符号整数）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteAsync(int startChannel, int[] values)
    {
        return Task.Run(() => Write(startChannel, values));
    }
}
