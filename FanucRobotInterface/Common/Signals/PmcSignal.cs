using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// <summary>PMC 信号（继电器 R / 保持继电器 K / 数据 D / 参数）。</summary>
/// </summary>
public class PmcSignal
{
    private readonly SnpxProtocol _client;

    internal PmcSignal(SnpxProtocol client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    // PMC 地址基址：Relay=index（无偏移），Keep=10000+index，Data=10000+index，Parameter=sdoIndex-11001+1
    private const int PmcKeepDataBase = 10000;

    // ---- Relay (R) ----
    /// <summary>读取单个 R 区内部继电器（同步）。</summary>
    /// <param name="index">继电器索引（从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public bool ReadRelay(int index) => ReadPmcBool(index);

    /// <summary>异步读取单个 R 区内部继电器。</summary>
    /// <param name="index">继电器索引（从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public Task<bool> ReadRelayAsync(int index) => Task.Run(() => ReadRelay(index));

    /// <summary>批量读取 R 区内部继电器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public bool[] ReadRelays(int startIndex, int count) => ReadPmcBools(startIndex, count);

    /// <summary>异步批量读取 R 区内部继电器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public Task<bool[]> ReadRelaysAsync(int startIndex, int count) => Task.Run(() => ReadRelays(startIndex, count));

    /// <summary>写入单个 R 区内部继电器（同步）。</summary>
    /// <param name="index">继电器索引（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteRelay(int index, bool value) => WritePmcBool(index, value);

    /// <summary>异步写入单个 R 区内部继电器。</summary>
    /// <param name="index">继电器索引（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteRelayAsync(int index, bool value) => Task.Run(() => WriteRelay(index, value));

    /// <summary>批量写入 R 区内部继电器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteRelays(int startIndex, bool[] values) => WritePmcBools(startIndex, values);

    /// <summary>异步批量写入 R 区内部继电器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteRelaysAsync(int startIndex, bool[] values) => Task.Run(() => WriteRelays(startIndex, values));

    // ---- Keep (K) ----
    /// <summary>读取单个 K 区保持继电器（同步）。</summary>
    /// <param name="index">保持继电器索引（从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public bool ReadKeep(int index) => ReadPmcBool(PmcKeepDataBase + index);

    /// <summary>异步读取单个 K 区保持继电器。</summary>
    /// <param name="index">保持继电器索引（从 1 开始）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public Task<bool> ReadKeepAsync(int index) => Task.Run(() => ReadKeep(index));

    /// <summary>批量读取 K 区保持继电器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public bool[] ReadKeeps(int startIndex, int count) => ReadPmcBools(PmcKeepDataBase + startIndex, count);

    /// <summary>异步批量读取 K 区保持继电器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>布尔值数组，true=ON，false=OFF。</returns>
    public Task<bool[]> ReadKeepsAsync(int startIndex, int count) => Task.Run(() => ReadKeeps(startIndex, count));

    /// <summary>写入单个 K 区保持继电器（同步）。</summary>
    /// <param name="index">保持继电器索引（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteKeep(int index, bool value) => WritePmcBool(PmcKeepDataBase + index, value);

    /// <summary>异步写入单个 K 区保持继电器。</summary>
    /// <param name="index">保持继电器索引（从 1 开始）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteKeepAsync(int index, bool value) => Task.Run(() => WriteKeep(index, value));

    /// <summary>批量写入 K 区保持继电器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteKeeps(int startIndex, bool[] values) => WritePmcBools(PmcKeepDataBase + startIndex, values);

    /// <summary>异步批量写入 K 区保持继电器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">布尔值数组，true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteKeepsAsync(int startIndex, bool[] values) => Task.Run(() => WriteKeeps(startIndex, values));

    // ---- Data (D) ----
    /// <summary>读取单个 D 区数据表寄存器（同步）。</summary>
    /// <param name="index">数据表索引（从 1 开始）。</param>
    /// <returns>16 位无符号整数值（0~65535）。</returns>
    public int ReadData(int index)
    {
        var values = _client.ReadInt(SnpxProtocol.SelectorPmcData, PmcKeepDataBase + index, 1);
        return values.Length > 0 ? values[0] : 0;
    }

    /// <summary>异步读取单个 D 区数据表寄存器。</summary>
    /// <param name="index">数据表索引（从 1 开始）。</param>
    /// <returns>16 位无符号整数值（0~65535）。</returns>
    public Task<int> ReadDataAsync(int index) => Task.Run(() => ReadData(index));

    /// <summary>批量读取 D 区数据表寄存器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>16 位无符号整数值数组。</returns>
    public int[] ReadDatas(int startIndex, int count)
        => _client.ReadInt(SnpxProtocol.SelectorPmcData, PmcKeepDataBase + startIndex, count);

    /// <summary>异步批量读取 D 区数据表寄存器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <returns>16 位无符号整数值数组。</returns>
    public Task<int[]> ReadDatasAsync(int startIndex, int count) => Task.Run(() => ReadDatas(startIndex, count));

    /// <summary>写入单个 D 区数据表寄存器（同步）。</summary>
    /// <param name="index">数据表索引（从 1 开始）。</param>
    /// <param name="value">要写入的值（0~65535）。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteData(int index, int value)
        => _client.WriteInt(SnpxProtocol.SelectorPmcData, PmcKeepDataBase + index, new[] { value });

    /// <summary>异步写入单个 D 区数据表寄存器。</summary>
    /// <param name="index">数据表索引（从 1 开始）。</param>
    /// <param name="value">要写入的值（0~65535）。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteDataAsync(int index, int value) => Task.Run(() => WriteData(index, value));

    /// <summary>批量写入 D 区数据表寄存器（同步）。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（0~65535）。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteDatas(int startIndex, int[] values)
        => _client.WriteInt(SnpxProtocol.SelectorPmcData, PmcKeepDataBase + startIndex, values);

    /// <summary>异步批量写入 D 区数据表寄存器。</summary>
    /// <param name="startIndex">起始索引（从 1 开始）。</param>
    /// <param name="values">要写入的值数组（0~65535）。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteDatasAsync(int startIndex, int[] values) => Task.Run(() => WriteDatas(startIndex, values));

    // ---- Parameter (SDO) ----
    /// <summary>
    /// 读取 PMC 参数（对应 SDO Index ≥ 11001 的映射）。
    /// 内部转换为 R 区继电器位访问（Index - 11001 + 1）。
    /// </summary>
    /// <param name="sdoIndex">SDO 索引（≥ 11001）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public bool ReadParameter(int sdoIndex) => ReadPmcBool(sdoIndex - 11001 + 1);

    /// <summary>异步读取 PMC 参数（对应 SDO Index ≥ 11001 的映射）。</summary>
    /// <param name="sdoIndex">SDO 索引（≥ 11001）。</param>
    /// <returns>true=ON，false=OFF。</returns>
    public Task<bool> ReadParameterAsync(int sdoIndex) => Task.Run(() => ReadParameter(sdoIndex));

    /// <summary>写入 PMC 参数（对应 SDO Index ≥ 11001 的映射）（同步）。</summary>
    /// <param name="sdoIndex">SDO 索引（≥ 11001）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool WriteParameter(int sdoIndex, bool value) => WritePmcBool(sdoIndex - 11001 + 1, value);

    /// <summary>异步写入 PMC 参数（对应 SDO Index ≥ 11001 的映射）。</summary>
    /// <param name="sdoIndex">SDO 索引（≥ 11001）。</param>
    /// <param name="value">true=ON，false=OFF。</param>
    /// <returns>写入成功返回 true。</returns>
    public Task<bool> WriteParameterAsync(int sdoIndex, bool value) => Task.Run(() => WriteParameter(sdoIndex, value));

    private bool ReadPmcBool(int address)
    {
        var values = _client.ReadBool(SnpxProtocol.SelectorPmcSignal, address, 1);
        return values.Length > 0 && values[0];
    }

    private bool[] ReadPmcBools(int startAddress, int count)
        => _client.ReadBool(SnpxProtocol.SelectorPmcSignal, startAddress, count);

    private bool WritePmcBool(int address, bool value)
        => _client.WriteBool(SnpxProtocol.SelectorPmcSignal, address, new[] { value });

    private bool WritePmcBools(int startAddress, bool[] values)
        => _client.WriteBool(SnpxProtocol.SelectorPmcSignal, startAddress, values);
}
