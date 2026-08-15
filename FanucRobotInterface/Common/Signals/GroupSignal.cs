using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// <summary>组信号（单字 16 位）。</summary>
/// </summary>
public class GroupSignal : SignalBase<int>
{
    private readonly SnpxProtocol _client;
    private readonly byte _selector;

    internal GroupSignal(SnpxProtocol client, SignalCategory category)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));

        Category = category;
        _selector = category == SignalCategory.GO
            ? SnpxProtocol.SelectorGroupOutput
            : SnpxProtocol.SelectorGroupInput;
    }

    /// <summary>读取单个组信号值（同步）。</summary>
    /// <param name="index">组信号索引（从 1 开始），如 GI[1]、GO[1]。</param>
    /// <returns>组信号值（0~65535）。</returns>
    public override int ReadSingle(int index)
    {
        var values = _client.ReadInt(_selector, index, 1);
        return values.Length > 0 ? values[0] : 0;
    }

    /// <summary>批量读取组信号值（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始），如 GI[1]~GI[10] 则 startIndex=1。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>组信号值数组（0~65535）。</returns>
    public override int[] Read(int startIndex, int count)
    {
        return _client.ReadInt(_selector, startIndex, count);
    }

    /// <summary>写入单个组信号值（同步）。</summary>
    /// <param name="index">组信号索引（从 1 开始）。</param>
    /// <param name="value">要写入的值（0~65535，超出范围会自动取模）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool WriteSingle(int index, int value)
    {
        return _client.WriteInt(_selector, index, new[] { value });
    }

    /// <summary>批量写入组信号值（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（0~65535，超出范围会自动取模）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool Write(int startIndex, int[] values)
    {
        return _client.WriteInt(_selector, startIndex, values);
    }

    /// <summary>异步读取单个组信号值。</summary>
    /// <param name="index">组信号索引（从 1 开始）。</param>
    /// <returns>组信号值（0~65535）。</returns>
    public override Task<int> ReadSingleAsync(int index)
    {
        return Task.Run(() => ReadSingle(index));
    }

    /// <summary>异步批量读取组信号值。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>组信号值数组（0~65535）。</returns>
    public override Task<int[]> ReadAsync(int startIndex, int count)
    {
        return Task.Run(() => Read(startIndex, count));
    }

    /// <summary>异步写入单个组信号值。</summary>
    /// <param name="index">组信号索引（从 1 开始）。</param>
    /// <param name="value">要写入的值（0~65535，超出范围会自动取模）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteSingleAsync(int index, int value)
    {
        return Task.Run(() => WriteSingle(index, value));
    }

    /// <summary>异步批量写入组信号值。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（0~65535，超出范围会自动取模）。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteAsync(int startIndex, int[] values)
    {
        return Task.Run(() => Write(startIndex, values));
    }
}
