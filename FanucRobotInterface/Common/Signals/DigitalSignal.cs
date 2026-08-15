using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// <summary>数字（位）信号。</summary>
/// </summary>
public class DigitalSignal : SignalBase<bool>
{
    private readonly SnpxProtocol _client;
    private readonly byte _selector;
    private readonly int _baseAddress;

    internal DigitalSignal(SnpxProtocol client, SignalCategory category)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));

        Category = category;

        switch (category)
        {
            case SignalCategory.DI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 0;
                break;
            case SignalCategory.DO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 0;
                break;
            case SignalCategory.RI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 5000;
                break;
            case SignalCategory.RO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 5000;
                break;
            case SignalCategory.UI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 6000;
                break;
            case SignalCategory.UO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 6000;
                break;
            case SignalCategory.SI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 7000;
                break;
            case SignalCategory.SO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 7000;
                break;
            case SignalCategory.WI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 8000;
                break;
            case SignalCategory.WO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 8000;
                break;
            case SignalCategory.WSI:
                _selector = SnpxProtocol.SelectorDigitalRead;
                _baseAddress = 8400;
                break;
            case SignalCategory.WSO:
                _selector = SnpxProtocol.SelectorDigitalWrite;
                _baseAddress = 8400;
                break;
            default:
                throw new ArgumentException($"不支持的数字信号类别:{category}", nameof(category));
        }
    }

    /// <summary>读取单个数字信号（同步）。</summary>
    /// <param name="index">信号索引（SI/SO 从 0 开始，其余从 1 开始），如 DI[1]、DO[1]、SI[0] 等。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public override bool ReadSingle(int index)
    {
        var values = _client.ReadBool(_selector, _baseAddress + index, 1);
        return values.Length > 0 && values[0];
    }

    /// <summary>批量读取数字信号（同步）。</summary>
    /// <param name="startIndex">起始索引（SI/SO 从 0 开始，其余从 1 开始），如 DI[1]~DI[10] 则 startIndex=1。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public override bool[] Read(int startIndex, int count)
    {
        return _client.ReadBool(_selector, _baseAddress + startIndex, count);
    }

    /// <summary>写入单个数字信号（同步）。</summary>
    /// <param name="index">信号索引（SI/SO 从 0 开始，其余从 1 开始），如 DO[1]=ON。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool WriteSingle(int index, bool value)
    {
        return _client.WriteBool(_selector, _baseAddress + index, new[] { value });
    }

    /// <summary>批量写入数字信号（同步）。</summary>
    /// <param name="startIndex">起始索引（SI/SO 从 0 开始，其余从 1 开始），如 DO[1]~DO[3] 则 startIndex=1。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public override bool Write(int startIndex, bool[] values)
    {
        return _client.WriteBool(_selector, _baseAddress + startIndex, values);
    }

    /// <summary>异步读取单个数字信号。</summary>
    /// <param name="index">信号索引（SI/SO 从 0 开始，其余从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public override Task<bool> ReadSingleAsync(int index)
    {
        return Task.Run(() => ReadSingle(index));
    }

    /// <summary>异步批量读取数字信号。</summary>
    /// <param name="startIndex">起始索引（SI/SO 从 0 开始，其余从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public override Task<bool[]> ReadAsync(int startIndex, int count)
    {
        return Task.Run(() => Read(startIndex, count));
    }

    /// <summary>异步写入单个数字信号。</summary>
    /// <param name="index">信号索引（SI/SO 从 0 开始，其余从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteSingleAsync(int index, bool value)
    {
        return Task.Run(() => WriteSingle(index, value));
    }

    /// <summary>异步批量写入数字信号。</summary>
    /// <param name="startIndex">起始索引（SI/SO 从 0 开始，其余从 1 开始）。</param>
    /// <param name="values">布尔值数组。</param>
    /// <returns>写入成功返回 true。</returns>
    public override Task<bool> WriteAsync(int startIndex, bool[] values)
    {
        return Task.Run(() => Write(startIndex, values));
    }
}
