using System;
using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotInterface.Common.Extensions;

/// <summary>
/// 位置数据（JointPosition / CartesianPosition / PositionInfo / PositionConfig）扩展方法。
/// 提供近似比较、判零、格式化显示、深拷贝、保留小数位和差值计算等功能。
/// </summary>
public static class PositionExtensions
{
    /// <summary>对关节坐标执行深拷贝。</summary>
    /// <param name="source">原始关节坐标。</param>
    /// <returns>新的 JointPosition 实例。</returns>
    public static JointPosition Clone(this JointPosition source)
    {
        return new JointPosition
        {
            J1 = source.J1, J2 = source.J2, J3 = source.J3,
            J4 = source.J4, J5 = source.J5, J6 = source.J6,
            J7 = source.J7, J8 = source.J8, J9 = source.J9
        };
    }

    /// <summary>对笛卡尔坐标执行深拷贝。</summary>
    /// <param name="source">原始笛卡尔坐标。</param>
    /// <returns>新的 CartesianPosition 实例。</returns>
    public static CartesianPosition Clone(this CartesianPosition source)
    {
        return new CartesianPosition
        {
            X = source.X, Y = source.Y, Z = source.Z,
            W = source.W, P = source.P, R = source.R,
            E1 = source.E1, E2 = source.E2, E3 = source.E3
        };
    }

    /// <summary>对完整位置信息执行深拷贝。</summary>
    /// <param name="source">原始位置信息。</param>
    /// <returns>新的 PositionInfo 实例。</returns>
    public static PositionInfo Clone(this PositionInfo source)
    {
        return new PositionInfo
        {
            Joint = source.Joint?.Clone(),
            Cartesian = source.Cartesian?.Clone(),
            Config = source.Config == null ? new PositionConfig() : Clone(source.Config),
            UF = source.UF,
            UT = source.UT,
            ValidCartesian = source.ValidCartesian,
            ValidJoint = source.ValidJoint
        };
    }

    private static PositionConfig Clone(PositionConfig c)
    {
        return new PositionConfig
        {
            NonFFlip = c.NonFFlip,
            LeftRight = c.LeftRight,
            DownUp = c.DownUp,
            BackTurn = c.BackTurn,
            Turn1 = c.Turn1,
            Turn2 = c.Turn2,
            Turn3 = c.Turn3
        };
    }

    /// <summary>计算两个关节坐标的差值绝对值。</summary>
    /// <param name="a">当前关节坐标。</param>
    /// <param name="b">要比较的另一个关节坐标。</param>
    /// <returns>各轴差值绝对值组成的新 JointPosition。</returns>
    public static JointPosition Diff(this JointPosition a, JointPosition b)
    {
        return new JointPosition
        {
            J1 = a.J1 - b.J1, J2 = a.J2 - b.J2, J3 = a.J3 - b.J3,
            J4 = a.J4 - b.J4, J5 = a.J5 - b.J5, J6 = a.J6 - b.J6,
            J7 = a.J7 - b.J7, J8 = a.J8 - b.J8, J9 = a.J9 - b.J9
        };
    }

    /// <summary>计算两个笛卡尔坐标的差值绝对值。</summary>
    /// <param name="a">当前笛卡尔坐标。</param>
    /// <param name="b">要比较的另一个笛卡尔坐标。</param>
    /// <returns>各分量差值绝对值组成的新 CartesianPosition。</returns>
    public static CartesianPosition Diff(this CartesianPosition a, CartesianPosition b)
    {
        return new CartesianPosition
        {
            X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z,
            W = a.W - b.W, P = a.P - b.P, R = a.R - b.R,
            E1 = a.E1 - b.E1, E2 = a.E2 - b.E2, E3 = a.E3 - b.E3
        };
    }

    /// <summary>判断关节坐标是否所有轴均为零。</summary>
    /// <param name="position">当前关节坐标。</param>
    /// <param name="tolerance">容差（默认 1e-3f）。</param>
    /// <returns>所有关节值的绝对值均小于 tolerance 时返回 true。</returns>
    public static bool IsZero(this JointPosition position, float tolerance = 0.0001f)
    {
        return Math.Abs(position.J1) <= tolerance && Math.Abs(position.J2) <= tolerance
            && Math.Abs(position.J3) <= tolerance && Math.Abs(position.J4) <= tolerance
            && Math.Abs(position.J5) <= tolerance && Math.Abs(position.J6) <= tolerance
            && Math.Abs(position.J7) <= tolerance && Math.Abs(position.J8) <= tolerance
            && Math.Abs(position.J9) <= tolerance;
    }

    /// <summary>判断两个关节坐标是否在容差内近似相等。</summary>
    /// <param name="a">当前关节坐标。</param>
    /// <param name="b">要比较的另一个关节坐标。</param>
    /// <param name="tolerance">容差（默认 1e-3f）。</param>
    /// <returns>所有关节值的差值绝对值均小于 tolerance 时返回 true。</returns>
    public static bool IsApproximately(this JointPosition a, JointPosition b, float tolerance = 0.0001f)
    {
        return Math.Abs(a.J1 - b.J1) <= tolerance && Math.Abs(a.J2 - b.J2) <= tolerance
            && Math.Abs(a.J3 - b.J3) <= tolerance && Math.Abs(a.J4 - b.J4) <= tolerance
            && Math.Abs(a.J5 - b.J5) <= tolerance && Math.Abs(a.J6 - b.J6) <= tolerance;
    }

    /// <summary>判断两个笛卡尔坐标是否在容差内近似相等。</summary>
    /// <param name="a">当前笛卡尔坐标。</param>
    /// <param name="b">要比较的另一个笛卡尔坐标。</param>
    /// <param name="tolerance">容差（默认 1e-3f）。</param>
    /// <returns>所有分量的差值绝对值均小于 tolerance 时返回 true。</returns>
    public static bool IsApproximately(this CartesianPosition a, CartesianPosition b, float tolerance = 0.0001f)
    {
        return Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance
            && Math.Abs(a.Z - b.Z) <= tolerance && Math.Abs(a.W - b.W) <= tolerance
            && Math.Abs(a.P - b.P) <= tolerance && Math.Abs(a.R - b.R) <= tolerance;
    }

    /// <summary>判断两个完整位置信息是否在容差内近似相等。</summary>
    /// <param name="a">当前位置信息。</param>
    /// <param name="b">要比较的另一个位置信息。</param>
    /// <param name="tolerance">容差（默认 1e-4f）。</param>
    /// <returns>关节坐标、笛卡尔坐标均近似相等且 UF/UT 相同时返回 true。</returns>
    public static bool IsApproximately(this PositionInfo a, PositionInfo b, float tolerance = 0.0001f)
    {
        return a.Joint.IsApproximately(b.Joint, tolerance)
            && a.Cartesian.IsApproximately(b.Cartesian, tolerance);
    }

    /// <summary>判断位置数据是否有效（笛卡尔和关节至少有一组有效）。</summary>
    /// <param name="position">位置信息。</param>
    /// <returns>ValidCartesian 或 ValidJoint 非零时返回 true。</returns>
    public static bool IsValid(this PositionInfo position)
    {
        return position != null
            && position.Cartesian != null
            && position.Joint != null
            && !float.IsNaN(position.Cartesian.X)
            && !float.IsNaN(position.Cartesian.Y)
            && !float.IsNaN(position.Cartesian.Z);
    }

    /// <summary>将关节坐标所有轴值保留指定小数位数。</summary>
    /// <param name="position">原始关节坐标。</param>
    /// <param name="decimals">小数位数（默认 3）。</param>
    /// <returns>保留指定位数后的新 JointPosition。</returns>
    public static JointPosition RoundTo(this JointPosition position, int decimals)
    {
        var result = position.Clone();
        result.J1 = result.J1.RoundTo(decimals);
        result.J2 = result.J2.RoundTo(decimals);
        result.J3 = result.J3.RoundTo(decimals);
        result.J4 = result.J4.RoundTo(decimals);
        result.J5 = result.J5.RoundTo(decimals);
        result.J6 = result.J6.RoundTo(decimals);
        result.J7 = result.J7.RoundTo(decimals);
        result.J8 = result.J8.RoundTo(decimals);
        result.J9 = result.J9.RoundTo(decimals);
        return result;
    }

    /// <summary>将笛卡尔坐标所有值保留指定小数位数。</summary>
    /// <param name="position">原始笛卡尔坐标。</param>
    /// <param name="decimals">小数位数（默认 3）。</param>
    /// <returns>保留指定位数后的新 CartesianPosition。</returns>
    public static CartesianPosition RoundTo(this CartesianPosition position, int decimals)
    {
        var result = position.Clone();
        result.X = result.X.RoundTo(decimals);
        result.Y = result.Y.RoundTo(decimals);
        result.Z = result.Z.RoundTo(decimals);
        result.W = result.W.RoundTo(decimals);
        result.P = result.P.RoundTo(decimals);
        result.R = result.R.RoundTo(decimals);
        result.E1 = result.E1.RoundTo(decimals);
        result.E2 = result.E2.RoundTo(decimals);
        result.E3 = result.E3.RoundTo(decimals);
        return result;
    }

    /// <summary>将位置信息中的所有坐标值保留指定小数位数。</summary>
    /// <param name="position">原始位置信息。</param>
    /// <param name="decimals">小数位数（默认 3）。</param>
    /// <returns>保留指定位数后的新 PositionInfo。</returns>
    public static PositionInfo RoundTo(this PositionInfo position, int decimals)
    {
        var result = position.Clone();
        result.Joint = position.Joint?.RoundTo(decimals);
        result.Cartesian = position.Cartesian?.RoundTo(decimals);
        return result;
    }

    /// <summary>将关节坐标格式化为可读字符串。</summary>
    /// <param name="position">当前关节坐标。</param>
    /// <param name="decimals">小数位数（默认 4 位）。</param>
    /// <returns>格式如 "J1=xxx.xxxx J2=xxx.xxxx ... J9=xxx.xxxx" 的字符串。</returns>
    public static string ToDisplayString(this JointPosition position, int decimals = 3)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append($"J1={position.J1.RoundTo(decimals)}");
        sb.Append($",J2={position.J2.RoundTo(decimals)}");
        sb.Append($",J3={position.J3.RoundTo(decimals)}");
        sb.Append($",J4={position.J4.RoundTo(decimals)}");
        sb.Append($",J5={position.J5.RoundTo(decimals)}");
        sb.Append($",J6={position.J6.RoundTo(decimals)}");
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>将笛卡尔坐标格式化为可读字符串。</summary>
    /// <param name="position">当前笛卡尔坐标。</param>
    /// <param name="decimals">小数位数（默认 3 位）。</param>
    /// <returns>格式如 "X=xxx.xxx Y=xxx.xxx Z=xxx.xxx W=xxx.xxx P=xxx.xxx R=xxx.xxx" 的字符串。</returns>
    public static string ToDisplayString(this CartesianPosition position, int decimals = 3)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append($"X={position.X.RoundTo(decimals)}");
        sb.Append($",Y={position.Y.RoundTo(decimals)}");
        sb.Append($",Z={position.Z.RoundTo(decimals)}");
        sb.Append($",W={position.W.RoundTo(decimals)}");
        sb.Append($",P={position.P.RoundTo(decimals)}");
        sb.Append($",R={position.R.RoundTo(decimals)}");
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>将完整位置信息格式化为可读字符串。</summary>
    /// <param name="position">位置信息。</param>
    /// <param name="decimals">小数位数（默认 3 位）。</param>
    /// <returns>包含 UF/UT、笛卡尔坐标、关节坐标和配置的多行字符串。</returns>
    public static string ToDisplayString(this PositionInfo position, int decimals = 3)
    {
        var sb = new StringBuilder();
        sb.Append($"Joint:{position.Joint?.ToDisplayString(decimals)}");
        sb.Append($" Cartesian:{position.Cartesian?.ToDisplayString(decimals)}");
        sb.Append($" [UF={position.UF}");
        sb.Append($" UT={position.UT}");
        sb.Append($" ValidC={position.ValidCartesian}");
        sb.Append($" ValidJ={position.ValidJoint}]");
        return sb.ToString();
    }

    /// <summary>将位置配置格式化为可读字符串。</summary>
    /// <param name="config">位置配置。</param>
    /// <returns>格式如 "D/U=... B/T=... Turn1=... Turn2=... Turn3=..." 的字符串。</returns>
    public static string ToDisplayString(this PositionConfig config)
    {
        var sb = new StringBuilder();
        sb.Append($"D/U={(short)config.DownUp}");
        sb.Append($" B/T={(short)config.BackTurn}");
        sb.Append($" Turn1={config.Turn1}");
        sb.Append($" Turn2={config.Turn2}");
        sb.Append($" Turn3={config.Turn3}");
        return sb.ToString();
    }
}
