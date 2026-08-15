namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 信号内存。客户端访问信号时，address = 类别偏移(baseOffset) + 逻辑索引。
/// 各类别 baseOffset：DI/DO=0、RI/RO=5000、UI/UO=6000、SI/SO=7000、WI/WO=8000、WSI/WSO=8400、
/// 模拟/组信号 baseOffset=channel（client 用 channel+1000）。
/// 位信号用 bool[]，模拟信号（AI/AO）用 int[]，组信号（GI/GO）用 ushort[]。
/// </summary>
public sealed class SignalStore
{
    private const int BitCapacity = 1024;
    private const int AnalogCapacity = 64;
    private const int GroupCapacity = 256;

    private readonly List<int> _bitBaseOffsets = new();
    private readonly List<int> _analogBaseOffsets = new();
    private readonly List<int> _groupBaseOffsets = new();
    // 位信号分为"输入区（I，selector=72 读）"与"输出区（Q，selector=70 写/读）"两块，真实控制器 DI 与 DO 相互独立。
    // key = baseOffset * 2 + (0=输入区 I, 1=输出区 Q)
    private readonly Dictionary<int, bool[]> _bitBlocks = new();
    // 模拟信号：AI(selector=12) 访问输入区，AO(selector=10) 访问输出区；key = baseOffset*2 + (0=I, 1=Q)
    private readonly Dictionary<int, int[]> _analogBlocks = new();
    // 组信号：GI(selector=12) 访问输入区，GO(selector=10) 访问输出区；key = baseOffset*2 + (0=I, 1=Q)
    private readonly Dictionary<int, ushort[]> _groupBlocks = new();

    private readonly object _sync = new();

    /// <summary>注册一个位信号类别（baseOffset 如 0/5000/6000...），同时注册输入区(I)与输出区(Q)。</summary>
    public void RegisterBitBlock(int baseOffset, int capacity = BitCapacity)
    {
        lock (_sync)
        {
            if (!_bitBlocks.ContainsKey(baseOffset * 2))
            {
                _bitBlocks[baseOffset * 2] = new bool[capacity];      // 输入区 I（DI/RI/UI/SI/WI/WSI）
                _bitBlocks[baseOffset * 2 + 1] = new bool[capacity];  // 输出区 Q（DO/RO/UO/SO/WO/WSO）
                _bitBaseOffsets.Add(baseOffset);
                _bitBaseOffsets.Sort();
            }
        }
    }

    /// <summary>注册一个模拟信号类别（baseOffset，如 1000），同时注册 AI 输入区与 AO 输出区。</summary>
    public void RegisterAnalogBlock(int baseOffset, int capacity = AnalogCapacity)
    {
        lock (_sync)
        {
            if (!_analogBlocks.ContainsKey(baseOffset * 2))
            {
                _analogBlocks[baseOffset * 2] = new int[capacity];     // AI 输入区
                _analogBlocks[baseOffset * 2 + 1] = new int[capacity]; // AO 输出区
                _analogBaseOffsets.Add(baseOffset);
                _analogBaseOffsets.Sort();
            }
        }
    }

    /// <summary>注册一个组信号类别（baseOffset），同时注册 GI 输入区与 GO 输出区。</summary>
    public void RegisterGroupBlock(int baseOffset, int capacity = GroupCapacity)
    {
        lock (_sync)
        {
            if (!_groupBlocks.ContainsKey(baseOffset * 2))
            {
                _groupBlocks[baseOffset * 2] = new ushort[capacity];     // GI 输入区
                _groupBlocks[baseOffset * 2 + 1] = new ushort[capacity]; // GO 输出区
                _groupBaseOffsets.Add(baseOffset);
                _groupBaseOffsets.Sort();
            }
        }
    }

    /// <summary>找到地址所属的位信号块（最大的 baseOffset ≤ address-1）。</summary>
    private bool TryResolveBitBlock(int address, bool isInput, out int baseOffset, out int localIndex)
    {
        baseOffset = -1;
        localIndex = -1;
        lock (_sync)
        {
            int zeroBased = address - 1;
            // 二分找最大 ≤ zeroBased 的 baseOffset
            int lo = 0, hi = _bitBaseOffsets.Count - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_bitBaseOffsets[mid] <= zeroBased)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            if (found < 0)
            {
                return false;
            }
            baseOffset = _bitBaseOffsets[found];
            localIndex = zeroBased - baseOffset;
            int key = baseOffset * 2 + (isInput ? 0 : 1);
            return _bitBlocks.ContainsKey(key);
        }
    }

    /// <summary>读取位信号。isInput=true 访问输入区（DI），false 访问输出区（DO）。</summary>
    public bool[] ReadBits(int address, int count, bool isInput)
    {
        if (!TryResolveBitBlock(address, isInput, out int baseOffset, out int localIndex))
        {
            return new bool[count];
        }
        lock (_sync)
        {
            var block = _bitBlocks[baseOffset * 2 + (isInput ? 0 : 1)];
            var result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    result[i] = block[pos];
                }
            }
            return result;
        }
    }

    /// <summary>写入位信号。isInput=true 写输入区（DI），false 写输出区（DO）。</summary>
    public void WriteBits(int address, bool[] values, bool isInput)
    {
        if (!TryResolveBitBlock(address, isInput, out int baseOffset, out int localIndex))
        {
            return;
        }
        lock (_sync)
        {
            var block = _bitBlocks[baseOffset * 2 + (isInput ? 0 : 1)];
            for (int i = 0; i < values.Length; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    block[pos] = values[i];
                }
            }
        }
    }

    /// <summary>读取模拟信号。isInput=true 访问 AI 输入区，false 访问 AO 输出区。</summary>
    public int[] ReadAnalog(int address, int count, bool isInput)
    {
        lock (_sync)
        {
            var result = new int[count];
            int zeroBased = address - 1;
            if (!FindAnalogBlock(zeroBased, isInput, out int blockOffset, out int localIndex))
            {
                return result;
            }
            var block = _analogBlocks[blockOffset];
            for (int i = 0; i < count; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    result[i] = block[pos];
                }
            }
            return result;
        }
    }

    /// <summary>写入模拟信号。isInput=true 写 AI 输入区，false 写 AO 输出区。</summary>
    public void WriteAnalog(int address, int[] values, bool isInput)
    {
        lock (_sync)
        {
            int zeroBased = address - 1;
            if (!FindAnalogBlock(zeroBased, isInput, out int blockOffset, out int localIndex))
            {
                return;
            }
            var block = _analogBlocks[blockOffset];
            for (int i = 0; i < values.Length; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    block[pos] = values[i];
                }
            }
        }
    }

    /// <summary>读取组信号。isInput=true 访问 GI 输入区，false 访问 GO 输出区。</summary>
    public ushort[] ReadGroup(int address, int count, bool isInput)
    {
        lock (_sync)
        {
            var result = new ushort[count];
            int zeroBased = address - 1;
            if (!FindGroupBlock(zeroBased, isInput, out int blockOffset, out int localIndex))
            {
                return result;
            }
            var block = _groupBlocks[blockOffset];
            for (int i = 0; i < count; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    result[i] = block[pos];
                }
            }
            return result;
        }
    }

    /// <summary>写入组信号。isInput=true 写 GI 输入区，false 写 GO 输出区。</summary>
    public void WriteGroup(int address, ushort[] values, bool isInput)
    {
        lock (_sync)
        {
            int zeroBased = address - 1;
            if (!FindGroupBlock(zeroBased, isInput, out int blockOffset, out int localIndex))
            {
                return;
            }
            var block = _groupBlocks[blockOffset];
            for (int i = 0; i < values.Length; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    block[pos] = values[i];
                }
            }
        }
    }

    private bool FindAnalogBlock(int zeroBased, bool isInput, out int blockOffset, out int localIndex)
    {
        blockOffset = -1;
        localIndex = -1;
        int best = -1;
        foreach (var key in _analogBaseOffsets)
        {
            if (key <= zeroBased && key > best)
            {
                best = key;
            }
        }
        if (best < 0)
        {
            return false;
        }
        int finalKey = best * 2 + (isInput ? 0 : 1);
        if (!_analogBlocks.ContainsKey(finalKey))
        {
            return false;
        }
        blockOffset = finalKey;
        localIndex = zeroBased - best;
        return true;
    }

    private bool FindGroupBlock(int zeroBased, bool isInput, out int blockOffset, out int localIndex)
    {
        blockOffset = -1;
        localIndex = -1;
        int best = -1;
        foreach (var key in _groupBaseOffsets)
        {
            if (key <= zeroBased && key > best)
            {
                best = key;
            }
        }
        if (best < 0)
        {
            return false;
        }
        int finalKey = best * 2 + (isInput ? 0 : 1);
        if (!_groupBlocks.ContainsKey(finalKey))
        {
            return false;
        }
        blockOffset = finalKey;
        localIndex = zeroBased - best;
        return true;
    }

    // ---- UI 辅助：按 baseOffset 直接读写 ----

    /// <summary>供 UI 读取位信号。isInput=true 读输入区（DI），false 读输出区（DO）。</summary>
    public bool GetBitByOffset(int baseOffset, int localIndex, bool isInput)
    {
        lock (_sync)
        {
            return _bitBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length && block[localIndex];
        }
    }

    /// <summary>供 UI 设置位信号。isInput=true 写输入区（DI），false 写输出区（DO）。</summary>
    public void SetBitByOffset(int baseOffset, int localIndex, bool value, bool isInput)
    {
        lock (_sync)
        {
            if (_bitBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length)
            {
                block[localIndex] = value;
            }
        }
    }

    /// <summary>供 UI 读取模拟信号。isInput=true 读 AI 输入区，false 读 AO 输出区。</summary>
    public int GetAnalogByOffset(int baseOffset, int localIndex, bool isInput)
    {
        lock (_sync)
        {
            return _analogBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length ? block[localIndex] : 0;
        }
    }

    /// <summary>供 UI 设置模拟信号。isInput=true 写 AI 输入区，false 写 AO 输出区。</summary>
    public void SetAnalogByOffset(int baseOffset, int localIndex, int value, bool isInput)
    {
        lock (_sync)
        {
            if (_analogBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length)
            {
                block[localIndex] = value;
            }
        }
    }

    /// <summary>供 UI 读取组信号。isInput=true 读 GI 输入区，false 读 GO 输出区。</summary>
    public ushort GetGroupByOffset(int baseOffset, int localIndex, bool isInput)
    {
        lock (_sync)
        {
            return _groupBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length ? block[localIndex] : (ushort)0;
        }
    }

    /// <summary>供 UI 设置组信号。isInput=true 写 GI 输入区，false 写 GO 输出区。</summary>
    public void SetGroupByOffset(int baseOffset, int localIndex, ushort value, bool isInput)
    {
        lock (_sync)
        {
            if (_groupBlocks.TryGetValue(baseOffset * 2 + (isInput ? 0 : 1), out var block)
                && localIndex >= 0 && localIndex < block.Length)
            {
                block[localIndex] = value;
            }
        }
    }

    // ---- PMC 信号（selector=76 位读写；地址规则：R 区=index，K 区=10000+index）----

    /// <summary>PMC 位信号容量（每区 1024 位）。</summary>
    private const int PmcBitCapacity = 1024;

    /// <summary>PMC D 区数据容量（16 位字）。</summary>
    private const int PmcDataCapacity = 256;

    // PMC 位信号按地址区间存储：key=0 表示 R 区（1..10000），key=1 表示 K 区（10001..）
    private readonly Dictionary<int, bool[]> _pmcBitBlocks = new();
    // PMC D 区数据（16 位无符号），地址 = 10000 + index
    private readonly int[] _pmcData = new int[PmcDataCapacity];
    private const int PmcKeepDataBase = 10000;

    /// <summary>注册 PMC 位信号区（调用一次即可，R 区与 K 区都初始化）。</summary>
    public void RegisterPmcBlocks()
    {
        lock (_sync)
        {
            if (!_pmcBitBlocks.ContainsKey(0))
            {
                _pmcBitBlocks[0] = new bool[PmcBitCapacity]; // R 区继电器
                _pmcBitBlocks[1] = new bool[PmcBitCapacity]; // K 区保持继电器
            }
        }
    }

    /// <summary>解析 PMC 位地址到区与局部索引。address=index(R区) 或 10000+index(K区)。</summary>
    private bool TryResolvePmcBit(int address, out int zone, out int localIndex)
    {
        zone = -1;
        localIndex = -1;
        int zeroBased;
        if (address > PmcKeepDataBase)
        {
            zone = 1; // K 区
            zeroBased = address - PmcKeepDataBase - 1;
        }
        else
        {
            zone = 0; // R 区
            zeroBased = address - 1;
        }
        if (zeroBased < 0)
        {
            return false;
        }
        localIndex = zeroBased;
        return true;
    }

    /// <summary>读取 PMC 位信号。</summary>
    public bool[] ReadPmcBits(int address, int count)
    {
        if (!TryResolvePmcBit(address, out int zone, out int localIndex))
        {
            return new bool[count];
        }
        lock (_sync)
        {
            if (!_pmcBitBlocks.TryGetValue(zone, out var block))
            {
                return new bool[count];
            }
            var result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    result[i] = block[pos];
                }
            }
            return result;
        }
    }

    /// <summary>写入 PMC 位信号。</summary>
    public void WritePmcBits(int address, bool[] values)
    {
        if (!TryResolvePmcBit(address, out int zone, out int localIndex))
        {
            return;
        }
        lock (_sync)
        {
            if (!_pmcBitBlocks.TryGetValue(zone, out var block))
            {
                return;
            }
            for (int i = 0; i < values.Length; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < block.Length)
                {
                    block[pos] = values[i];
                }
            }
        }
    }

    /// <summary>读取 PMC D 区数据（16 位字，地址=10000+index）。</summary>
    public int[] ReadPmcData(int address, int count)
    {
        lock (_sync)
        {
            var result = new int[count];
            int localIndex = address - PmcKeepDataBase - 1;
            for (int i = 0; i < count; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < _pmcData.Length)
                {
                    result[i] = _pmcData[pos];
                }
            }
            return result;
        }
    }

    /// <summary>写入 PMC D 区数据（16 位字，地址=10000+index）。</summary>
    public void WritePmcData(int address, int[] values)
    {
        lock (_sync)
        {
            int localIndex = address - PmcKeepDataBase - 1;
            for (int i = 0; i < values.Length; i++)
            {
                int pos = localIndex + i;
                if (pos >= 0 && pos < _pmcData.Length)
                {
                    _pmcData[pos] = values[i] & 0xFFFF;
                }
            }
        }
    }

    // ---- UI 辅助：PMC 按区 + 局部索引读写 ----

    /// <summary>供 UI 读取 PMC 位信号。zone=0 表示 R 区，zone=1 表示 K 区。</summary>
    public bool GetPmcBitByOffset(int zone, int localIndex)
    {
        lock (_sync)
        {
            return _pmcBitBlocks.TryGetValue(zone, out var block)
                && localIndex >= 0 && localIndex < block.Length && block[localIndex];
        }
    }

    /// <summary>供 UI 设置 PMC 位信号。zone=0 表示 R 区，zone=1 表示 K 区。</summary>
    public void SetPmcBitByOffset(int zone, int localIndex, bool value)
    {
        lock (_sync)
        {
            if (_pmcBitBlocks.TryGetValue(zone, out var block)
                && localIndex >= 0 && localIndex < block.Length)
            {
                block[localIndex] = value;
            }
        }
    }

    /// <summary>供 UI 读取 PMC D 区数据。</summary>
    public int GetPmcDataByOffset(int localIndex)
    {
        lock (_sync)
        {
            return localIndex >= 0 && localIndex < _pmcData.Length ? _pmcData[localIndex] : 0;
        }
    }

    /// <summary>供 UI 设置 PMC D 区数据。</summary>
    public void SetPmcDataByOffset(int localIndex, int value)
    {
        lock (_sync)
        {
            if (localIndex >= 0 && localIndex < _pmcData.Length)
            {
                _pmcData[localIndex] = value & 0xFFFF;
            }
        }
    }
}
