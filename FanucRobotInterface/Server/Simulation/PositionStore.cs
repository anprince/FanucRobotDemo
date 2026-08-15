using System.Buffers.Binary;
using FanucRobotInterface.Common.Data;

namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 位置 / 系统变量存储：维护 POS、PR、$VAR 等变量名的词表，并提供与 PositionInfo 的互转。
/// 词表布局与 PositionDataManager/PosRegManager 一致（50 字位置块）。
/// </summary>
public sealed class PositionStore
{
    /// <summary>位置数据块固定 50 字。</summary>
    public const int PositionWords = 50;

    private readonly Dictionary<string, short[]> _positions = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>读取位置变量词表（不存在则返回全零 50 字）。</summary>
    public short[] Read(string variableName)
    {
        lock (_sync)
        {
            if (_positions.TryGetValue(variableName, out var words))
            {
                return (short[])words.Clone();
            }
            return new short[PositionWords];
        }
    }

    /// <summary>写入位置变量词表（自动扩展到 50 字）。</summary>
    public void Write(string variableName, short[] words)
    {
        lock (_sync)
        {
            var block = new short[PositionWords];
            Array.Copy(words, block, Math.Min(words.Length, PositionWords));
            _positions[variableName] = block;
        }
    }

    /// <summary>把位置词表解析为 PositionInfo。</summary>
    public static PositionInfo Parse(short[] data)
    {
        var result = new PositionInfo();
        if (data.Length < 12)
        {
            return result;
        }
        result.Cartesian.X = ShortsToFloat(data, 0);
        result.Cartesian.Y = ShortsToFloat(data, 2);
        result.Cartesian.Z = ShortsToFloat(data, 4);
        result.Cartesian.W = ShortsToFloat(data, 6);
        result.Cartesian.P = ShortsToFloat(data, 8);
        result.Cartesian.R = ShortsToFloat(data, 10);
        if (data.Length < PositionWords)
        {
            return result;
        }
        result.Cartesian.E1 = ShortsToFloat(data, 12);
        result.Cartesian.E2 = ShortsToFloat(data, 14);
        result.Cartesian.E3 = ShortsToFloat(data, 16);
        result.Config.NonFFlip = (PositionConfig.FlipState)data[18];
        result.Config.LeftRight = (PositionConfig.HandConfig)data[19];
        result.Config.DownUp = (PositionConfig.ArmConfig)data[20];
        result.Config.BackTurn = (PositionConfig.TurnConfig)data[21];
        result.Config.Turn1 = data[22];
        result.Config.Turn2 = data[23];
        result.Config.Turn3 = data[24];
        result.ValidCartesian = data[25];
        result.Joint.J1 = ShortsToFloat(data, 26);
        result.Joint.J2 = ShortsToFloat(data, 28);
        result.Joint.J3 = ShortsToFloat(data, 30);
        result.Joint.J4 = ShortsToFloat(data, 32);
        result.Joint.J5 = ShortsToFloat(data, 34);
        result.Joint.J6 = ShortsToFloat(data, 36);
        result.Joint.J7 = ShortsToFloat(data, 38);
        result.Joint.J8 = ShortsToFloat(data, 40);
        result.Joint.J9 = ShortsToFloat(data, 42);
        result.ValidJoint = data[44];
        result.UF = data[45];
        result.UT = data[46];
        return result;
    }

    /// <summary>把 PositionInfo 序列化为 50 字位置词表。</summary>
    public static short[] ToWords(PositionInfo p)
    {
        var words = new short[PositionWords];
        if (p == null)
        {
            return words;
        }
        var c = p.Cartesian;
        if (c != null)
        {
            WriteFloat(words, 0, c.X);
            WriteFloat(words, 2, c.Y);
            WriteFloat(words, 4, c.Z);
            WriteFloat(words, 6, c.W);
            WriteFloat(words, 8, c.P);
            WriteFloat(words, 10, c.R);
            WriteFloat(words, 12, c.E1);
            WriteFloat(words, 14, c.E2);
            WriteFloat(words, 16, c.E3);
        }
        if (p.Config != null)
        {
            words[18] = (short)p.Config.NonFFlip;
            words[19] = (short)p.Config.LeftRight;
            words[20] = (short)p.Config.DownUp;
            words[21] = (short)p.Config.BackTurn;
            words[22] = p.Config.Turn1;
            words[23] = p.Config.Turn2;
            words[24] = p.Config.Turn3;
        }
        words[25] = p.ValidCartesian;
        var j = p.Joint;
        if (j != null)
        {
            WriteFloat(words, 26, j.J1);
            WriteFloat(words, 28, j.J2);
            WriteFloat(words, 30, j.J3);
            WriteFloat(words, 32, j.J4);
            WriteFloat(words, 34, j.J5);
            WriteFloat(words, 36, j.J6);
            WriteFloat(words, 38, j.J7);
            WriteFloat(words, 40, j.J8);
            WriteFloat(words, 42, j.J9);
        }
        words[44] = p.ValidJoint;
        words[45] = p.UF;
        words[46] = p.UT;
        return words;
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

    private static void WriteFloat(short[] words, int index, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        words[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0));
        words[index + 1] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
    }
}
