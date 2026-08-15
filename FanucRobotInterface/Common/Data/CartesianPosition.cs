namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>笛卡尔位置（X/Y/Z/W/P/R + 扩展轴 E1-E3）。</summary>
/// </summary>
public class CartesianPosition
{
    /// <summary>X 坐标。</summary>
    public float X { get; set; }
    /// <summary>Y 坐标。</summary>
    public float Y { get; set; }
    /// <summary>Z 坐标。</summary>
    public float Z { get; set; }
    /// <summary>绕 X 轴旋转（W）。</summary>
    public float W { get; set; }
    /// <summary>绕 Y 轴旋转（P）。</summary>
    public float P { get; set; }
    /// <summary>绕 Z 轴旋转（R）。</summary>
    public float R { get; set; }
    /// <summary>外部轴 1 坐标。</summary>
    public float E1 { get; set; }
    /// <summary>外部轴 2 坐标。</summary>
    public float E2 { get; set; }
    /// <summary>外部轴 3 坐标。</summary>
    public float E3 { get; set; }
}
