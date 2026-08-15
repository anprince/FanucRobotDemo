namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// <summary>信号类别。</summary>
/// </summary>
public enum SignalCategory
{
    /// <summary>数字输入（DI）。</summary>
    DI,
    /// <summary>数字输出（DO）。</summary>
    DO,
    /// <summary>机器人输入（RI）。</summary>
    RI,
    /// <summary>机器人输出（RO）。</summary>
    RO,
    /// <summary>用户输入（UI）。</summary>
    UI,
    /// <summary>用户输出（UO）。</summary>
    UO,
    /// <summary>信号输入（SI）。</summary>
    SI,
    /// <summary>信号输出（SO）。</summary>
    SO,
    /// <summary>组输入（GI）。</summary>
    GI,
    /// <summary>组输出（GO）。</summary>
    GO,
    /// <summary>模拟输入（AI）。</summary>
    AI,
    /// <summary>模拟输出（AO）。</summary>
    AO,
    /// <summary>焊接输入（WI）。</summary>
    WI,
    /// <summary>焊接输出（WO）。</summary>
    WO,
    /// <summary>焊接系统输入（WSI）。</summary>
    WSI,
    /// <summary>焊接系统输出（WSO）。</summary>
    WSO,
    /// <summary>PMC 信号。</summary>
    PMC
}
