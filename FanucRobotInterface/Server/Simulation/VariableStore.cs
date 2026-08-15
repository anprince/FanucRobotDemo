using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace FanucRobotInterface.Server.Simulation;

/// <summary>
/// 变量表 + %R 地址映射表。
/// 客户端通过 SETASG 把逻辑变量（R[1]、SR[1]、F[1]、POS[1]、$SYSNAME、ALM[E1]、PRG[1] 等）
/// 映射到 %R 起始地址；随后对 %R 的读写被解析回对应变量的词表（short[]）。
/// </summary>
public sealed class VariableStore
{
    /// <summary>%R DataTable 上限（words）。</summary>
    public const int MaxWords = 16384;

    /// <summary>%R → 变量区间 的映射表（按起始地址排序）。</summary>
    private readonly SortedDictionary<int, Binding> _bindings = new();

    /// <summary>变量名 → 词表。词表独立于 %R 物理布局，读写 %R 时按区间映射。</summary>
    private readonly Dictionary<string, Variable> _variables = new(StringComparer.Ordinal);

    private readonly object _sync = new();

    /// <summary>绑定表只读快照（供 UI 展示）。</summary>
    public ReadOnlyCollection<BindingInfo> Bindings { get; private set; } = new(Array.Empty<BindingInfo>());

    /// <summary>变量只读快照（供 UI 展示）。</summary>
    public ReadOnlyCollection<VariableInfo> Variables { get; private set; } = new(Array.Empty<VariableInfo>());

    /// <summary>
    /// 处理一条 SETASG/CLRASG 命令。成功返回 true。
    /// </summary>
    /// <param name="command">命令字符串（不含末尾 NUL）。</param>
    public bool ProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return true;
        }

        string trimmed = command.Trim('\0').Trim();
        if (trimmed.Equals("CLRASG", StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return true;
        }

        if (!trimmed.StartsWith("SETASG", StringComparison.OrdinalIgnoreCase))
        {
            // 其他命令（如 CLRALM）忽略
            return true;
        }

        // 格式：SETASG <addr> <words> <variable> <fmt>
        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int address)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int words))
        {
            return false;
        }

        string variableName = parts[3];
        SetBinding(address, words, variableName);
        return true;
    }

    /// <summary>建立 %R 起始地址 → 变量 的绑定。若变量已存在则复用其词表。</summary>
    private void SetBinding(int address, int words, string variableName)
    {
        lock (_sync)
        {
            // R[i] 共享 "R[1]" 容器，绑定需按 offset 映射到 R[1] 的 Words[(i-1)*2..]
            string canonical = CanonicalRName(variableName);
            int rOffset = RIndexOffset(variableName);
            if (rOffset >= 0)
            {
                // 计算 R[1] 所需总容量：起始 R[i] 的 offset 到 offset+words 至少能覆盖
                int required = rOffset + words;
                // 移除旧绑定中与该区间重叠的部分
                RemoveOverlapping(address, words);
                if (!_variables.TryGetValue("R[1]", out var v))
                {
                    // 默认 100 word 容器（足够覆盖 50 个 R[i]）
                    v = new Variable("R[1]", Math.Max(required, 100));
                    _variables["R[1]"] = v;
                }
                else if (v.Words.Length < required)
                {
                    Array.Resize(ref v.Words, Math.Max(required, v.Words.Length * 2));
                }
                _bindings[address] = new Binding(address, words, v, rOffset);
                RefreshSnapshots();
                return;
            }

            // 非 R 系列：原有逻辑
            RemoveOverlapping(address, words);
            if (!_variables.TryGetValue(canonical, out var variable))
            {
                variable = new Variable(canonical, words);
                _variables[canonical] = variable;
            }
            else if (variable.Words.Length < words)
            {
                Array.Resize(ref variable.Words, words);
            }
            _bindings[address] = new Binding(address, words, variable);
            RefreshSnapshots();
        }
    }

    /// <summary>移除与 [address, address+words) 重叠的旧绑定。</summary>
    private void RemoveOverlapping(int address, int words)
    {
        int end = address + words - 1;
        var toRemove = new List<int>();
        foreach (var kv in _bindings)
        {
            int bEnd = kv.Key + kv.Value.Words - 1;
            if (kv.Key <= end && bEnd >= address)
            {
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _bindings.Remove(key);
        }
    }

    /// <summary>清空所有绑定（CLRASG）。变量词表保留，便于下次绑定快速重建。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _bindings.Clear();
            RefreshSnapshots();
        }
    }

    /// <summary>
    /// 读取 %R [address, address+count) 对应的词表数据。
    /// 若未绑定，返回全零。
    /// </summary>
    public short[] Read(int address, int count)
    {
        lock (_sync)
        {
            var result = new short[count];
            ReadInto(result, 0, address, count);
            return result;
        }
    }

    /// <summary>读取并写入目标数组 dst 的 dstOffset 起，源 %R 从 address 起 count 字。</summary>
    private void ReadInto(short[] dst, int dstOffset, int address, int count)
    {
        // 遍历可能覆盖该区间的绑定
        foreach (var kv in _bindings)
        {
            int bindStart = kv.Key;
            int bindEnd = kv.Key + kv.Value.Words - 1;
            int reqEnd = address + count - 1;
            if (bindEnd < address || bindStart > reqEnd)
            {
                continue;
            }
            // 交集范围
            int ovlStart = Math.Max(bindStart, address);
            int ovlEnd = Math.Min(bindEnd, reqEnd);
            for (int i = ovlStart; i <= ovlEnd; i++)
            {
                int srcWord = kv.Value.VariableOffset + (i - bindStart);
                if (srcWord < kv.Value.Variable.Words.Length)
                {
                    dst[dstOffset + (i - address)] = kv.Value.Variable.Words[srcWord];
                }
            }
        }
    }

    /// <summary>
    /// 写入 %R [address, address+count)，数据落入对应绑定变量的词表。
    /// </summary>
    public void Write(int address, short[] values)
    {
        lock (_sync)
        {
            bool changed = false;
            foreach (var kv in _bindings)
            {
                int bindStart = kv.Key;
                int bindEnd = kv.Key + kv.Value.Words - 1;
                int reqEnd = address + values.Length - 1;
                if (bindEnd < address || bindStart > reqEnd)
                {
                    continue;
                }
                int ovlStart = Math.Max(bindStart, address);
                int ovlEnd = Math.Min(bindEnd, reqEnd);
                for (int i = ovlStart; i <= ovlEnd; i++)
                {
                    int dstWord = kv.Value.VariableOffset + (i - bindStart);
                    int srcWord = i - address;
                    if (dstWord < kv.Value.Variable.Words.Length && kv.Value.Variable.Words[dstWord] != values[srcWord])
                    {
                        kv.Value.Variable.Words[dstWord] = values[srcWord];
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                RefreshSnapshots();
            }
        }
    }

    /// <summary>按变量名读取词表副本。R[i] 返回共享 "R[1]" 变量中的 2 字切片。</summary>
    public bool TryGetVariableWords(string name, out short[]? words)
    {
        lock (_sync)
        {
            string canonical = CanonicalRName(name);
            if (_variables.TryGetValue(canonical, out var v))
            {
                int offset = RIndexOffset(name);
                if (offset >= 0)
                {
                    int len = Math.Max(0, Math.Min(v.Words.Length - offset, 2));
                    if (len <= 0)
                    {
                        words = null;
                        return false;
                    }
                    var slice = new short[len];
                    Array.Copy(v.Words, offset, slice, 0, len);
                    words = slice;
                    return true;
                }
                words = (short[])v.Words.Clone();
                return true;
            }
            words = null;
            return false;
        }
    }

    /// <summary>查找地址所属绑定变量名（供特殊类型处理，如 ALM/PRG/POS 需要专用存储）。</summary>
    public bool TryGetBindingName(int address, out string name)
    {
        lock (_sync)
        {
            return TryGetBindingInfoInternal(address, out _, out name);
        }
    }

    /// <summary>查找地址所属绑定的起始地址与变量名（供按偏移读写专用存储）。</summary>
    public bool TryGetBindingInfo(int address, out int startAddress, out string name)
    {
        lock (_sync)
        {
            return TryGetBindingInfoInternal(address, out startAddress, out name);
        }
    }

    private bool TryGetBindingInfoInternal(int address, out int startAddress, out string name)
    {
        // 找覆盖 address 的绑定
        foreach (var kv in _bindings)
        {
            if (address >= kv.Key && address < kv.Key + kv.Value.Words)
            {
                startAddress = kv.Key;
                name = kv.Value.Variable.Name;
                return true;
            }
        }
        startAddress = 0;
        name = string.Empty;
        return false;
    }

    /// <summary>按变量名写回其词表（供 UI 编辑后同步）。变量不存在时按名称自动创建默认容量。</summary>
    public bool TrySetVariableWords(string name, short[] words)
    {
        lock (_sync)
        {
            // R 系列共享 "R[1]" 变量（客户端 SETASG "R[1]" 60 words 表示 R[1..30]），
            // 单个 R[i] 也映射到 R[1] 变量的 (i-1)*2..(i-1)*2+1 区间。
            string canonical = CanonicalRName(name);
            if (!_variables.TryGetValue(canonical, out var v))
            {
                int capacity = InferDefaultCapacity(canonical, words.Length);
                v = new Variable(canonical, capacity);
                _variables[canonical] = v;
            }
            // 计算目标偏移（R[i] → (i-1)*2）
            int offset = RIndexOffset(name);
            if (offset >= 0)
            {
                for (int i = 0; i < words.Length && (offset + i) < v.Words.Length; i++)
                {
                    v.Words[offset + i] = words[i];
                }
            }
            else
            {
                Array.Copy(words, v.Words, Math.Min(words.Length, v.Words.Length));
            }
            RefreshSnapshots();
            return true;
        }
    }

    /// <summary>
    /// 枚举所有注释变量（键名匹配 {prefix}[C{数字}]，如 R[C1]、DI[C5]），解码文本并返回有序列表。
    /// 供注释列表 UI 实时展示默认注释与客户端写入的修改。
    /// </summary>
    public List<CommentEntry> GetAllComments()
    {
        lock (_sync)
        {
            var result = new List<CommentEntry>();
            foreach (var kv in _variables)
            {
                if (IsCommentName(kv.Key))
                {
                    result.Add(new CommentEntry(kv.Key, DecodeText(kv.Value.Words)));
                }
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return result;
        }
    }

    /// <summary>判断是否为注释变量（格式 {prefix}[C{index}]）。与 SimulatedController.IsCommentVariable 判断逻辑一致。</summary>
    private static bool IsCommentName(string name)
    {
        int open = name.IndexOf('[');
        int close = name.IndexOf(']');
        if (open <= 0 || close < open + 2)
        {
            return false;
        }
        string inner = name.Substring(open + 1, close - open - 1);
        return inner.Length > 1 && inner[0] == 'C' && int.TryParse(inner.Substring(1), out _);
    }

    /// <summary>将 short 词表解码为文本（小端 short→byte + NUL 截断，与 CommentManager/RegisterSignalViewModel 一致）。</summary>
    private static string DecodeText(short[] words)
    {
        var bytes = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            bytes[i * 2] = (byte)(words[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((words[i] >> 8) & 0xFF);
        }
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }
        return Common.SnpxProtocol.DefaultStringEncoding.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>将 R[i] 映射到 "R[1]" 共享变量；其他名称原样返回。</summary>
    private static string CanonicalRName(string name)
    {
        if (name.StartsWith("R[", StringComparison.Ordinal) && TryParseBracketedIndex(name, out _))
        {
            return "R[1]";
        }
        return name;
    }

    /// <summary>R[i] 返回 (i-1)*2，否则 -1。</summary>
    private static int RIndexOffset(string name)
    {
        if (name.StartsWith("R[", StringComparison.Ordinal) && TryParseBracketedIndex(name, out int idx))
        {
            return (idx - 1) * 2;
        }
        return -1;
    }

    private static bool TryParseBracketedIndex(string name, out int index)
    {
        int end = name.IndexOf(']');
        if (end > 2 && int.TryParse(name.Substring(2, end - 2), out index) && index >= 1)
        {
            return true;
        }
        index = 0;
        return false;
    }

    /// <summary>按变量名前缀推断默认词容量（UI 编辑时使用）。</summary>
    private static int InferDefaultCapacity(string name, int fallback)
    {
        if (string.IsNullOrEmpty(name))
        {
            return fallback;
        }
        if (name.StartsWith("R[", StringComparison.Ordinal) || name.StartsWith("PR[", StringComparison.Ordinal))
        {
            // 位置/数值寄存器默认 50 字（位置）或 2 字（数值）—— 客户端常规 R[i] 写入 2 字
            return name.StartsWith("R[", StringComparison.Ordinal) ? Math.Max(2, fallback) : Math.Max(50, fallback);
        }
        if (name.StartsWith("F[", StringComparison.Ordinal))
        {
            return Math.Max(1, fallback);
        }
        if (name.StartsWith("SR[", StringComparison.Ordinal))
        {
            return Math.Max(40, fallback);
        }
        if (name.StartsWith("$", StringComparison.Ordinal))
        {
            // 系统变量：按类型默认（INT/REAL=2，STRING=40，POS=50）
            return Math.Max(2, fallback);
        }
        return fallback;
    }

    private void RefreshSnapshots()
    {
        Bindings = new ReadOnlyCollection<BindingInfo>(_bindings.Select(kv => new BindingInfo(kv.Key, kv.Value.Words, kv.Value.Variable.Name)).ToList());
        Variables = new ReadOnlyCollection<VariableInfo>(_variables.Select(kv => new VariableInfo(kv.Key, kv.Value.Words.Length)).ToList());
    }

    /// <summary>变量实体。</summary>
    private sealed class Variable
    {
        public string Name { get; }
        public short[] Words;

        public Variable(string name, int words)
        {
            Name = name;
            Words = new short[words];
        }
    }

    /// <summary>%R 地址区间 → 变量 绑定。</summary>
    private sealed class Binding
    {
        public int Address { get; }
        public int Words { get; }
        public Variable Variable { get; }
        /// <summary>变量词表内偏移（R[i] 共享 R[1] 时 = (i-1)*2；其他为 0）。</summary>
        public int VariableOffset { get; }

        public Binding(int address, int words, Variable variable, int variableOffset = 0)
        {
            Address = address;
            Words = words;
            Variable = variable;
            VariableOffset = variableOffset;
        }
    }
}

/// <summary>绑定表 UI 展示项。</summary>
public sealed class BindingInfo
{
    public int Address { get; }
    public int Words { get; }
    public string Variable { get; }

    public BindingInfo(int address, int words, string variable)
    {
        Address = address;
        Words = words;
        Variable = variable;
    }
}

/// <summary>变量表 UI 展示项。</summary>
public sealed class VariableInfo
{
    public string Name { get; }
    public int Words { get; }

    public VariableInfo(string name, int words)
    {
        Name = name;
        Words = words;
    }
}

/// <summary>注释变量 UI 展示项（变量名 + 注释文本）。</summary>
public sealed class CommentEntry
{
    /// <summary>原始变量名，如 DI[1]。</summary>
    public string Name { get; }

    /// <summary>展示用变量名，去掉注释索引 C，如 DI[1]、R[1]。</summary>
    public string DisplayName { get; }

    /// <summary>注释文本。</summary>
    public string Text { get; }

    public CommentEntry(string name, string text)
    {
        Name = name;
        Text = text;
        DisplayName = ToDisplayName(name);
    }

    /// <summary>把 {前缀}[C{索引}] 转为 {前缀}[{索引}]（去掉 C）。</summary>
    private static string ToDisplayName(string name)
    {
        int open = name.IndexOf('[');
        int close = name.IndexOf(']');
        if (open > 0 && close > open + 2 && name[open + 1] == 'C')
        {
            // 保留 '['，跳过 'C'：例如 "DI[C1]" → "DI[1]"
            return name.Substring(0, open + 1) + name.Substring(open + 2);
        }
        return name;
    }
}
