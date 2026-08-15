using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// 位置寄存器管理器（PR[i]），支持多运动组 PR[G{group}:{index}]
/// 每个 PR 固定 50 words，包含 Cartesian + Config + Joint + UF/UT
/// group=1：PR[{index}]，group>=2：PR[G{group}:{index}]
/// 写入模式：
/// WriteJoint(Async) — 只写关节值 + UF/UT，机器人自动反算笛卡尔
/// WriteCartesian(Async) — 只写笛卡尔值 + Config + UF/UT，机器人自动反算关节
/// WriteJointBatch(Async) / WriteCartesianBatch(Async) — 批量写入，支持逐 PR 独立指定 UF/UT/Group
/// </summary>
public class PosRegManager
{
    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<(int group, int index), int> _cache = new();

    internal PosRegManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    private const int PositionWords = 50;  // PR 位置数据块固定 50 字

    // ---- 读取 ----

    /// <summary>
    /// 同步读取位置寄存器 PR[{group}:{index}]。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>包含关节坐标、笛卡尔坐标和配置的完整位置信息。</returns>
    public PositionInfo Read(int index, int group = 1)
    {
        int address = GetOrBindAddress(index, group);
        var words = _client.ReadRegisters(address, PositionWords);
        return ParsePosition(words);
    }

    /// <summary>
    /// 异步读取位置寄存器 PR[{group}:{index}] 的完整位置信息。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为多运动组 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>包含关节坐标、笛卡尔坐标和配置的完整位置信息。</returns>
    public Task<PositionInfo> ReadAsync(int index, int group = 1) => Task.Run(() => Read(index, group));

    /// <summary>
    /// 同步批量读取位置寄存器。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <param name="group">运动组编号（1=默认组）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>位置信息数组。</returns>
    public PositionInfo[] ReadBatch(int startIndex, int count, int group = 1)
    {
        var result = new PositionInfo[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = Read(startIndex + i, group);
        }
        return result;
    }

    /// <summary>
    /// 异步批量读取位置寄存器 PR[{group}:{startIndex}]~PR[{group}:{startIndex+count-1}]。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="count">读取数量。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    /// <returns>位置信息数组。</returns>
    public Task<PositionInfo[]> ReadBatchAsync(int startIndex, int count, int group = 1)
        => Task.Run(() => ReadBatch(startIndex, count, group));

    // ---- 写关节位置 ----

    /// <summary>
    /// 同步写入关节坐标到 PR[{group}:{index}]。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="joint">关节坐标（J1~J9）。</param>
    /// <param name="uf">用户坐标系编号（0~15，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    public void WriteJoint(int index, JointPosition joint, short uf = 1, short ut = 1, int group = 1)
    {
        int address = GetOrBindAddress(index, group);
        WriteJointData(address, joint, uf, ut);
    }

    /// <summary>
    /// 异步写入关节坐标到 PR[{group}:{index}]，机器人自动反算笛卡尔坐标。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="joint">关节坐标（J1~J9）。</param>
    /// <param name="uf">用户坐标系编号（0~15，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    public Task WriteJointAsync(int index, JointPosition joint, short uf = 1, short ut = 1, int group = 1)
        => Task.Run(() => WriteJoint(index, joint, uf, ut, group));

    // ---- 写笛卡尔位置 ----

    /// <summary>
    /// 同步写入笛卡尔坐标到 PR[{group}:{index}]。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesian">笛卡尔坐标（X, Y, Z, W, P, R）。</param>
    /// <param name="config">位置配置（翻转、手臂上下、手腕回转、回转圈数）。</param>
    /// <param name="uf">用户坐标系编号（0~15，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    public void WriteCartesian(int index, CartesianPosition cartesian, PositionConfig config, short uf = 1, short ut = 1, int group = 1)
    {
        int address = GetOrBindAddress(index, group);
        WriteCartesianData(address, cartesian, config, uf, ut);
    }

    /// <summary>
    /// 异步写入笛卡尔坐标到 PR[{group}:{index}]，机器人自动反算关节坐标。
    /// </summary>
    /// <param name="index">位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesian">笛卡尔坐标（X, Y, Z, W, P, R）。</param>
    /// <param name="config">位置配置（翻转、手臂上下、手腕回转、回转圈数）。</param>
    /// <param name="uf">用户坐标系编号（0~15，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，>=2 时为 PR[G{group}:{index}]）。该组未配置时不会抛出异常，数据可能无效。</param>
    public Task WriteCartesianAsync(int index, CartesianPosition cartesian, PositionConfig config, short uf = 1, short ut = 1, int group = 1)
        => Task.Run(() => WriteCartesian(index, cartesian, config, uf, ut, group));

    // ---- 批量写 ----

    /// <summary>
    /// 同步批量写入关节坐标（所有 PR 同组同 UF/UT）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="joints">关节坐标数组。</param>
    /// <param name="uf">用户坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，所有 PR 共用）。该组未配置时不会抛出异常，写入可能无效。</param>
    public void WriteJointBatch(int startIndex, JointPosition[] joints, short uf = 1, short ut = 1, int group = 1)
    {
        var ufs = new short[joints.Length];
        var uts = new short[joints.Length];
        var groups = new int[joints.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            ufs[i] = uf; uts[i] = ut; groups[i] = group;
        }
        WriteJointBatch(startIndex, joints, ufs, uts, groups);
    }

    /// <summary>
    /// 异步批量写入关节坐标（所有 PR 同组同 UF/UT）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="joints">关节坐标数组。</param>
    /// <param name="uf">用户坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，所有 PR 共用）。该组未配置时不会抛出异常，写入可能无效。</param>
    public Task WriteJointBatchAsync(int startIndex, JointPosition[] joints, short uf = 1, short ut = 1, int group = 1)
        => Task.Run(() => WriteJointBatch(startIndex, joints, uf, ut, group));

    /// <summary>
    /// 同步批量写入关节坐标（逐 PR 独立指定 UF/UT/Group）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="joints">关节坐标数组。</param>
    /// <param name="ufs">用户坐标系编号数组（长度必须与 joints 一致）。</param>
    /// <param name="uts">工具坐标系编号数组（长度必须与 joints 一致）。</param>
    /// <param name="groups">运动组编号数组（长度必须与 joints 一致）。</param>
    public void WriteJointBatch(int startIndex, JointPosition[] joints, short[] ufs, short[] uts, int[] groups)
    {
        if (joints.Length == 0) return;

        // groups 全相同且容量足够 → 批量绑定：分配连续块 + 一条 SETASG + 逐个写 21 字关节
        if (AllSame(groups) && _allocator.RemainingWords >= joints.Length * PositionWords)
        {
            int totalWords = joints.Length * PositionWords;
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} {BuildVariable(startIndex, groups[0])} 0.0");
            for (int i = 0; i < joints.Length; i++)
            {
                _cache[(groups[i], startIndex + i)] = addr + i * PositionWords;
                WriteJointData(addr + i * PositionWords, joints[i], ufs[i], uts[i]);
            }
            return;
        }

        // 否则逐个写
        for (int i = 0; i < joints.Length; i++)
        {
            WriteJoint(startIndex + i, joints[i], ufs[i], uts[i], groups[i]);
        }
    }

    /// <summary>
    /// 异步批量写入关节坐标（逐 PR 独立指定 UF/UT/Group）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="joints">关节坐标数组。</param>
    /// <param name="ufs">用户坐标系编号数组（长度必须与 joints 一致）。</param>
    /// <param name="uts">工具坐标系编号数组（长度必须与 joints 一致）。</param>
    /// <param name="groups">运动组编号数组（长度必须与 joints 一致）。</param>
    public Task WriteJointBatchAsync(int startIndex, JointPosition[] joints, short[] ufs, short[] uts, int[] groups)
        => Task.Run(() => WriteJointBatch(startIndex, joints, ufs, uts, groups));

    /// <summary>
    /// 同步批量写入笛卡尔坐标（所有 PR 同组同 UF/UT）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesians">笛卡尔坐标数组。</param>
    /// <param name="configs">位置配置数组（长度必须与 cartesians 一致）。</param>
    /// <param name="uf">用户坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，所有 PR 共用）。该组未配置时不会抛出异常，写入可能无效。</param>
    public void WriteCartesianBatch(int startIndex, CartesianPosition[] cartesians, PositionConfig[] configs, short uf = 1, short ut = 1, int group = 1)
    {
        var ufs = new short[cartesians.Length];
        var uts = new short[cartesians.Length];
        var groups = new int[cartesians.Length];
        for (int i = 0; i < cartesians.Length; i++)
        {
            ufs[i] = uf; uts[i] = ut; groups[i] = group;
        }
        WriteCartesianBatch(startIndex, cartesians, configs, ufs, uts, groups);
    }

    /// <summary>
    /// 异步批量写入笛卡尔坐标（所有 PR 同组同 UF/UT）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesians">笛卡尔坐标数组。</param>
    /// <param name="configs">位置配置数组（长度必须与 cartesians 一致）。</param>
    /// <param name="uf">用户坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="ut">工具坐标系编号（0~15，所有 PR 共用，默认 1）。</param>
    /// <param name="group">运动组编号（1=默认组，所有 PR 共用）。该组未配置时不会抛出异常，写入可能无效。</param>
    public Task WriteCartesianBatchAsync(int startIndex, CartesianPosition[] cartesians, PositionConfig[] configs, short uf = 1, short ut = 1, int group = 1)
        => Task.Run(() => WriteCartesianBatch(startIndex, cartesians, configs, uf, ut, group));

    /// <summary>
    /// 同步批量写入笛卡尔坐标（逐 PR 独立指定 UF/UT/Group）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesians">笛卡尔坐标数组。</param>
    /// <param name="configs">位置配置数组（长度必须与 cartesians 一致）。</param>
    /// <param name="ufs">用户坐标系编号数组（长度必须与 cartesians 一致）。</param>
    /// <param name="uts">工具坐标系编号数组（长度必须与 cartesians 一致）。</param>
    /// <param name="groups">运动组编号数组（长度必须与 cartesians 一致）。</param>
    public void WriteCartesianBatch(int startIndex, CartesianPosition[] cartesians, PositionConfig[] configs, short[] ufs, short[] uts, int[] groups)
    {
        if (cartesians.Length == 0) return;

        // groups 全相同且容量足够 → 批量绑定：分配连续块 + 一条 SETASG + 逐个写
        if (AllSame(groups) && _allocator.RemainingWords >= cartesians.Length * PositionWords)
        {
            int totalWords = cartesians.Length * PositionWords;
            int addr = _allocator.Allocate(totalWords);
            _client.SendCommand($"SETASG {addr} {totalWords} {BuildVariable(startIndex, groups[0])} 0.0");
            for (int i = 0; i < cartesians.Length; i++)
            {
                _cache[(groups[i], startIndex + i)] = addr + i * PositionWords;
                WriteCartesianData(addr + i * PositionWords, cartesians[i], configs[i], ufs[i], uts[i]);
            }
            return;
        }

        // 否则逐个写
        for (int i = 0; i < cartesians.Length; i++)
        {
            WriteCartesian(startIndex + i, cartesians[i], configs[i], ufs[i], uts[i], groups[i]);
        }
    }

    /// <summary>
    /// 异步批量写入笛卡尔坐标（逐 PR 独立指定 UF/UT/Group）。
    /// </summary>
    /// <param name="startIndex">起始位置寄存器编号（从 1 开始）。</param>
    /// <param name="cartesians">笛卡尔坐标数组。</param>
    /// <param name="configs">位置配置数组（长度必须与 cartesians 一致）。</param>
    /// <param name="ufs">用户坐标系编号数组（长度必须与 cartesians 一致）。</param>
    /// <param name="uts">工具坐标系编号数组（长度必须与 cartesians 一致）。</param>
    /// <param name="groups">运动组编号数组（长度必须与 cartesians 一致）。</param>
    public Task WriteCartesianBatchAsync(int startIndex, CartesianPosition[] cartesians, PositionConfig[] configs, short[] ufs, short[] uts, int[] groups)
        => Task.Run(() => WriteCartesianBatch(startIndex, cartesians, configs, ufs, uts, groups));

    private static bool AllSame(int[] groups)
    {
        if (groups.Length <= 1) return true;
        int first = groups[0];
        for (int i = 1; i < groups.Length; i++)
        {
            if (groups[i] != first) return false;
        }
        return true;
    }

    // ---- 内部 ----

    private int GetOrBindAddress(int index, int group)
    {
        var key = (group, index);
        if (_cache.TryGetValue(key, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(PositionWords);
        string variable = BuildVariable(index, group);
        string command = $"SETASG {address} {PositionWords} {variable} 0.0";
        _client.SendCommand(command);
        _cache[key] = address;
        return address;
    }

    private static string BuildVariable(int index, int group)
        => group > 1 ? $"PR[G{group}:{index}]" : $"PR[{index}]";

    /// <summary>写关节数据（21 字：J1..J9 + ValidJoint(0) + UF + UT）到 addr+26。</summary>
    private void WriteJointData(int address, JointPosition joint, short uf, short ut)
    {
        var words = new short[21];
        WriteFloat(words, 0, joint.J1);
        WriteFloat(words, 2, joint.J2);
        WriteFloat(words, 4, joint.J3);
        WriteFloat(words, 6, joint.J4);
        WriteFloat(words, 8, joint.J5);
        WriteFloat(words, 10, joint.J6);
        WriteFloat(words, 12, joint.J7);
        WriteFloat(words, 14, joint.J8);
        WriteFloat(words, 16, joint.J9);
        words[18] = 0;      // ValidJoint
        words[19] = uf;     // UF
        words[20] = ut;     // UT
        _client.WriteRegisters(address + 26, words);
    }

    /// <summary>写笛卡尔数据（26 字：9 float + config 7 字段 + ValidCartesian(1)）到 addr，UF/UT 写到 addr+45。</summary>
    private void WriteCartesianData(int address, CartesianPosition cartesian, PositionConfig config, short uf, short ut)
    {
        var words = new short[26];
        WriteFloat(words, 0, cartesian.X);
        WriteFloat(words, 2, cartesian.Y);
        WriteFloat(words, 4, cartesian.Z);
        WriteFloat(words, 6, cartesian.W);
        WriteFloat(words, 8, cartesian.P);
        WriteFloat(words, 10, cartesian.R);
        WriteFloat(words, 12, cartesian.E1);
        WriteFloat(words, 14, cartesian.E2);
        WriteFloat(words, 16, cartesian.E3);

        words[18] = (short)config.NonFFlip;
        words[19] = (short)config.LeftRight;
        words[20] = (short)config.DownUp;
        words[21] = (short)config.BackTurn;
        words[22] = config.Turn1;
        words[23] = config.Turn2;
        words[24] = config.Turn3;
        words[25] = 1;  // ValidCartesian

        _client.WriteRegisters(address, words);

        // UF/UT 写到 address+45（2 字）
        _client.WriteRegisters(address + 45, new[] { uf, ut });
    }

    /// <summary>解析 PR 位置数据（50 字），布局同 PositionDataManager。</summary>
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

        result.Config.NonFFlip = (PositionConfig.FlipState)data[18];
        result.Config.LeftRight = (PositionConfig.HandConfig)data[19];
        result.Config.DownUp = (PositionConfig.ArmConfig)data[20];
        result.Config.BackTurn = (PositionConfig.TurnConfig)data[21];
        result.Config.Turn1 = data[22];
        result.Config.Turn2 = data[23];
        result.Config.Turn3 = data[24];

        return result;
    }

    private static float ShortsToFloat(short[] words, int index)
    {
        if (words.Length < index + 2)
        {
            return 0f;
        }
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), words[index]);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), words[index + 1]);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static void WriteFloat(short[] words, int index, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        words[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0));
        words[index + 1] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
    }
}
