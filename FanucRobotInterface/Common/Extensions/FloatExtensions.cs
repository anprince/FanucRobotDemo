using System;

namespace FanucRobotInterface.Common.Extensions;

/// <summary>
/// <summary>float 扩展方法。</summary>
/// </summary>
public static class FloatExtensions
{
    /// <summary>将 float 值钳制到 [min, max] 范围内。</summary>
    /// <param name="value">原始值。</param>
    /// <param name="min">最小值。</param>
    /// <param name="max">最大值。</param>
    /// <returns>钳制后的值。</returns>
    public static float Clamp(this float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }
        if (value > max)
        {
            return max;
        }
        return value;
    }

    /// <summary>判断 float 值是否在 [min, max] 范围内（含边界）。</summary>
    /// <param name="value">当前值。</param>
    /// <param name="min">范围下限。</param>
    /// <param name="max">范围上限。</param>
    /// <returns>如果在范围内则返回 true。</returns>
    public static bool IsInRange(this float value, float min, float max)
    {
        return value >= min && value <= max;
    }

    /// <summary>判断两个 float 值是否在指定容差内近似相等。</summary>
    /// <param name="value">当前值。</param>
    /// <param name="other">要比较的值。</param>
    /// <param name="epsilon">容差（默认为 1e-6f）。</param>
    /// <returns>如果差值绝对值小于 epsilon 则返回 true。</returns>
    public static bool IsApproximately(this float value, float other, float epsilon = 0.0001f)
    {
        return Math.Abs(value - other) <= epsilon;
    }

    /// <summary>将 float 值保留指定小数位数（严格四舍五入）。</summary>
    /// <param name="value">原始浮点数。</param>
    /// <param name="decimals">小数位数（默认为 3）。</param>
    /// <returns>保留指定位数后的 float 值。</returns>
    public static float RoundTo(this float value, int decimals)
    {
        return (float)Math.Round(value, decimals);
    }

    /// <summary>将 float 数组中的每个元素保留指定小数位数（严格四舍五入）。</summary>
    /// <param name="values">浮点数数组。</param>
    /// <param name="decimals">小数位数（默认为 3）。</param>
    /// <returns>四舍五入后的 float 数组。</returns>
    public static float[] RoundAll(this float[] values, int decimals)
    {
        var result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = values[i].RoundTo(decimals);
        }
        return result;
    }

    /// <summary>将 float 值格式化为指定小数位数的字符串（严格四舍五入）。</summary>
    /// <param name="value">原始浮点数。</param>
    /// <param name="decimals">小数位数（默认为 3）。</param>
    /// <returns>格式化后的字符串。</returns>
    public static string ToFormattedString(this float value, int decimals)
    {
        return value.ToString("F" + decimals);
    }
}
