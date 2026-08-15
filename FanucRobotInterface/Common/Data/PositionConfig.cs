namespace FanucRobotInterface.Common.Data;

/// <summary>
/// 配置字数据。
/// 根据 FANUC SNPX POS 协议，Word 18-24 为配置字：
/// Flip, LeftRight, UpDown, FrontBack, Turn1, Turn2, Turn3。
/// 参考：FANUC SNPX 协议规范、robotif_dot_net_eng.pdf。
/// </summary>
public class PositionConfig
{
    /// <summary>手腕翻转状态。</summary>
    public enum FlipState : short
    {
        /// <summary>非翻转（NonFlip）。</summary>
        NonFlip,
        /// <summary>翻转（Flip）。</summary>
        Flip
    }

    /// <summary>手臂左右配置。</summary>
    public enum HandConfig : short
    {
        /// <summary>左手（Left）。</summary>
        Left,
        /// <summary>右手（Right）。</summary>
        Right
    }

    /// <summary>手臂上下配置。</summary>
    public enum ArmConfig : short
    {
        /// <summary>手臂向下（Down）。</summary>
        Down,
        /// <summary>手臂向上（Up）。</summary>
        Up
    }

    /// <summary>
    /// 手腕回转方位（Turn/Back）。
    /// FANUC 配置字符串第三位：T=前侧(Turn/Front), B=后侧(Back)。
    /// </summary>
    public enum TurnConfig : short
    {
        /// <summary>后侧（Back）。</summary>
        Back,
        /// <summary>前侧（Turn）。</summary>
        Turn
    }

    /// <summary>翻转状态（0=NonFlip, 1=Flip）。</summary>
    public FlipState NonFFlip { get; set; }

    /// <summary>手臂左右（0=Left, 1=Right）。在机器人的示教器中未显示此类型</summary>
    public HandConfig LeftRight { get; set; }

    /// <summary>手臂上下（0=Down, 1=Up）。</summary>
    public ArmConfig DownUp { get; set; }

    /// <summary>手腕回转方位（0=Back, 1=Turn）。</summary>
    public TurnConfig BackTurn { get; set; }

    /// <summary>关节 1 回转圈数。</summary>
    public short Turn1 { get; set; }

    /// <summary>关节 2 回转圈数。</summary>
    public short Turn2 { get; set; }

    /// <summary>关节 3 回转圈数。</summary>
    public short Turn3 { get; set; }
}
