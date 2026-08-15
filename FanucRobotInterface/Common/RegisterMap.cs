using System;

namespace FanucRobotInterface.Common;

/// <summary>
/// SNPX 协议 %R 寄存器地址中央分配器。
/// 所有需要 SETASG 的功能模块（PositionDataManager、SystemVariablesManager、NumRegManager 等）
/// 都通过此分配器统一获取 %R 起始地址，避免地址冲突。
/// </summary>
/// <remarks>
/// SNPX 协议通过 SETASG 指令将系统数据映射到 %R 寄存器区域。
/// DataTable 内部 %R 寄存器上限为 16384 words（0x4000），
/// 本分配器预留 384 words 余量，单页上限设为 16000 words。
/// 参考：FANUC SNPX 协议规范、robotif_dot_net_eng.pdf。
/// </remarks>
public class RegisterMap
{
    /// <summary>
    /// 单页最大字数。DataTable 内部 %R 寄存器限制为 16384 words，
    /// 预留 384 words 余量以确保安全。
    /// </summary>
    public const int MaxWords = 16000;

    /// <summary>
    /// 中央临时槽位大小（2000 words）。
    /// 所有模块共享此区域用于：
    /// 1) 永久缓存满时的回退读写
    /// 2) 分页批量读取的临时缓冲区
    /// 不占用永久缓存额度。
    /// </summary>
    public const int CentralTempSlotSize = 2000;

    private const int ReservedBase = 1;  // 临时槽起始地址（地址 1）

    private int _cursor;       // 当前分配游标（1-based）
    private int _usedWords;    // 已用字数
    private readonly int _centralTempSlotAddress;  // 中央临时槽起始地址
    private int _currentPage;  // 当前页号

    /// <summary>
    /// 中央临时槽位起始 %R 地址。
    /// 构造时自动从 %R1 开始分配 CentralTempSlotSize 个 words。
    /// 各模块通过此地址进行临时读写操作。
    /// </summary>
    public int CentralTempSlotAddress => _centralTempSlotAddress;

    /// <summary>
    /// 当前页号（从 0 开始）。
    /// 每页对应 DataTable 的一个缓存页，发送 CLRASG 后调用 NewPage() 切换。
    /// </summary>
    public int CurrentPage => _currentPage;

    /// <summary>
    /// <summary>当前已分配的地址总数（含中央临时槽位）。</summary>
    /// </summary>
    public int UsedWords => _cursor - 1;

    /// <summary>
    /// 当前页剩余可用字数（不含中央临时槽位）。
    /// 即永久缓存区剩余的可分配字数。
    /// </summary>
    public int RemainingWords => MaxWords - (_cursor - 1);

/// <summary>初始化实例。</summary>
    public RegisterMap()
    {
        // 构造时分配 2000 字中央临时槽（地址 1..2000），游标从 2001 开始
        _cursor = ReservedBase + CentralTempSlotSize;
        _centralTempSlotAddress = ReservedBase;
        _currentPage = 0;
    }

    /// <summary>
    /// 分配指定字数的连续 %R 地址。
    /// 从中央临时槽位之后的永久缓存区开始分配。
    /// </summary>
    /// <param name="words">需要分配的字数（word 数），必须大于 0。</param>
    /// <returns>1-based %R 起始地址。</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">words 小于等于 0 时抛出。</exception>
    /// <exception cref="T:System.InvalidOperationException">当前页地址空间不足时抛出。</exception>
    public int Allocate(int words)
    {
        if (words <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(words), "分配字数必须大于 0");
        }

        if (_cursor + words > MaxWords + 1)
        {
            throw new InvalidOperationException($"地址空间不足：当前页 {_currentPage}，需 {words} 字，已用 {_cursor - 1}，上限 {MaxWords}");
        }

        int address = _cursor;
        _cursor += words;
        if (_cursor > _usedWords)
        {
            _usedWords = _cursor;
        }
        return address;
    }

    /// <summary>
    /// 切换到新页。
    /// </summary>
    /// <remarks>
    /// 切换前调用方必须先发送 CLRASG 指令清除当前 DataTable 的寄存器映射。
    /// 切换后 _currentAddress 重置到中央临时槽位之后（%R2001），
    /// 永久缓存区重新开始分配。
    /// </remarks>
    public void NewPage()
    {
        _cursor = ReservedBase + CentralTempSlotSize;
        _currentPage++;
    }

    /// <summary>
    /// 重置分配器（连接断开或重连时调用）。
    /// 中央临时槽位（%R1..%R2000）保持不变，永久缓存从 %R2001 重新开始分配。
    /// </summary>
    public void Reset()
    {
        _cursor = ReservedBase + CentralTempSlotSize;
        _currentPage = 0;
        _usedWords = _cursor;
    }
}
