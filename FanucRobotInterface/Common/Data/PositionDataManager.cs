using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>当前位置数据管理器。</summary>
/// </summary>
public class PositionDataManager
{
    private const int PositionWords = 50;  // 位置数据块固定 50 字

    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<(int group, int uf), int> _cache = new();

    internal PositionDataManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    /// <summary>读取世界坐标系下的笛卡尔坐标（uf=0）。</summary>
    /// <param name="group">运动组编号（默认 1）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>笛卡尔坐标数据。</returns>
    public CartesianPosition ReadWorldPosition(int group = 1)
        => ReadPosition(0, group).Cartesian;

    /// <summary>异步读取世界坐标系下的笛卡尔坐标（uf=0）。</summary>
    /// <param name="group">运动组编号（默认 1）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>笛卡尔坐标数据。</returns>
    public Task<CartesianPosition> ReadWorldPositionAsync(int group = 1)
        => Task.Run(() => ReadWorldPosition(group));

    /// <summary>读取关节坐标。</summary>
    /// <param name="group">运动组编号（默认 1）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>关节坐标数据。</returns>
    public JointPosition ReadJointPosition(int group = 1)
        => ReadPosition(0, group).Joint;

    /// <summary>异步读取关节坐标。</summary>
    /// <param name="group">运动组编号（默认 1）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>关节坐标数据。</returns>
    public Task<JointPosition> ReadJointPositionAsync(int group = 1)
        => Task.Run(() => ReadJointPosition(group));

    /// <summary>读取指定用户坐标系下的完整位置信息。</summary>
    /// <param name="ufNumber">用户坐标系编号（1-9）。</param>
    /// <param name="group">运动组编号（默认 1）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>完整位置信息。</returns>
    public PositionInfo ReadUserPosition(int ufNumber, int group = 1)
    {
        if (ufNumber < 1 || ufNumber > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(ufNumber), "用户坐标系编号必须在 1-9 之间");
        }
        return ReadPosition(ufNumber, group);
    }

    /// <summary>异步读取指定用户坐标系下的完整位置信息。</summary>
    /// <param name="ufNumber">用户坐标系编号（1-9）。</param>
    /// <param name="group">运动组编号（1=默认组，2+=外部轴组）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>完整位置信息。</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">ufNumber 不在 1-9 范围内时抛出。</exception>
    public Task<PositionInfo> ReadUserPositionAsync(int ufNumber, int group = 1)
        => Task.Run(() => ReadUserPosition(ufNumber, group));

    private PositionInfo ReadPosition(int uf, int group)
    {
        int address = GetOrBindAddress(group, uf);
        var words = _client.ReadRegisters(address, PositionWords);
        return ParsePosition(words);
    }

    private int GetOrBindAddress(int group, int uf)
    {
        var key = (group, uf);
        if (_cache.TryGetValue(key, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(PositionWords);
        string variable = group > 1
            ? $"POS[G{group}:{uf}]"
            : $"POS[{uf}]";
        string command = $"SETASG {address} {PositionWords} {variable} 0.0";
        _client.SendCommand(command);
        _cache[key] = address;
        return address;
    }

    /// <summary>解析位置数据（50 字）。float 占 2 字（小端）。</summary>
    private static PositionInfo ParsePosition(short[] data)
    {
        if (data.Length < PositionWords)
        {
            return new PositionInfo();
        }

        var result = new PositionInfo
        {
            Cartesian = new CartesianPosition
            {
                X = ShortsToFloat(data, 0),
                Y = ShortsToFloat(data, 2),
                Z = ShortsToFloat(data, 4),
                W = ShortsToFloat(data, 6),
                P = ShortsToFloat(data, 8),
                R = ShortsToFloat(data, 10),
                E1 = ShortsToFloat(data, 12),
                E2 = ShortsToFloat(data, 14),
                E3 = ShortsToFloat(data, 16)
            },
            Joint = new JointPosition
            {
                J1 = ShortsToFloat(data, 26),
                J2 = ShortsToFloat(data, 28),
                J3 = ShortsToFloat(data, 30),
                J4 = ShortsToFloat(data, 32),
                J5 = ShortsToFloat(data, 34),
                J6 = ShortsToFloat(data, 36),
                J7 = ShortsToFloat(data, 38),
                J8 = ShortsToFloat(data, 40),
                J9 = ShortsToFloat(data, 42)
            },
            ValidCartesian = data[25],
            ValidJoint = data[44],
            UF = data[45],
            UT = data[46]
        };

        // 配置字段（[18..24]）
        result.Config.NonFFlip = (PositionConfig.FlipState)data[18];
        result.Config.LeftRight = (PositionConfig.HandConfig)data[19];
        result.Config.DownUp = (PositionConfig.ArmConfig)data[20];
        result.Config.BackTurn = (PositionConfig.TurnConfig)data[21];
        result.Config.Turn1 = data[22];
        result.Config.Turn2 = data[23];
        result.Config.Turn3 = data[24];

        return result;
    }

    private static float ShortsToFloat(short[] data, int index)
    {
        if (data.Length < index + 2)
        {
            return 0f;
        }
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), data[index]);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), data[index + 1]);
        return BitConverter.ToSingle(bytes, 0);
    }
}
