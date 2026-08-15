namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>位置信息（关节 + 笛卡尔 + 配置）。</summary>
/// </summary>
public class PositionInfo
{
    /// <summary>关节坐标。</summary>
    public JointPosition Joint { get; set; }

    /// <summary>笛卡尔坐标。</summary>
    public CartesianPosition Cartesian { get; set; }

    /// <summary>位置配置。</summary>
    public PositionConfig Config { get; set; }

    /// <summary>用户坐标系编号。</summary>
    public short UF { get; set; }

    /// <summary>工具坐标系编号。</summary>
    public short UT { get; set; }

    /// <summary>笛卡尔坐标有效性标志（0=无效，1=有效）。</summary>
    public short ValidCartesian { get; set; }

    /// <summary>关节坐标有效性标志（0=无效，1=有效）。</summary>
    public short ValidJoint { get; set; }

/// <summary>初始化实例。</summary>
    public PositionInfo()
    {
        Joint = new JointPosition();
        Cartesian = new CartesianPosition();
        Config = new PositionConfig();
    }
}
