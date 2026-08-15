using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using FanucRobotInterface.Server.Simulation;

namespace FanucRobotServerSH.ViewModels;

/// <summary>
/// 注释编辑页 ViewModel：注释编辑（{前缀}[C{索引}]）+ 注释列表实时展示。
/// 列表监听 DataChanged（kind=="variable"），客户端写入注释后自动刷新，直观看到修改效果。
/// </summary>
public sealed class CommentViewModel : ViewModelBase
{
    private readonly SimulatedController _controller;
    private readonly Dispatcher _dispatcher;
    private string _commentIndex = "1";
    private string _commentValue = "";

    public CommentViewModel(SimulatedController controller, Dispatcher dispatcher)
    {
        _controller = controller;
        _dispatcher = dispatcher;
        LoadCommentCommand = new RelayCommandNoArg(LoadComment);
        ApplyCommentCommand = new RelayCommand(ApplyComment);
        RefreshCommentsCommand = new RelayCommandNoArg(RefreshComments);
        ApplyRowCommand = new RelayCommand(p => ApplyRow(p as CommentRow));
        controller.DataChanged += OnDataChanged;

        // 初始化即展示本地默认注释
        RefreshComments();
    }

    public string CommentIndex { get => _commentIndex; set => SetProperty(ref _commentIndex, value); }
    public string CommentValue { get => _commentValue; set => SetProperty(ref _commentValue, value); }

    /// <summary>注释类型下拉列表（与 CommentManager.CommentType 一致的常见前缀）。</summary>
    public IReadOnlyList<string> CommentTypes { get; } = new[]
    {
        "R", "PR", "SR", "DI", "DO", "RI", "RO", "UI", "UO",
        "SI", "SO", "GI", "GO", "AI", "AO", "WI", "WO", "F"
    };

    /// <summary>按类型分组的注释列表（每个类型一组，含类型标题 + 变量/注释条目），随 DataChanged 实时刷新。</summary>
    public ObservableCollection<CommentGroup> CommentGroups { get; } = new();

    /// <summary>当前选中的注释类型前缀（默认 R）。</summary>
    public string SelectedCommentType { get; set; } = "R";

    public RelayCommandNoArg LoadCommentCommand { get; }
    public RelayCommand ApplyCommentCommand { get; }
    public RelayCommandNoArg RefreshCommentsCommand { get; }
    /// <summary>表格内按行写入注释命令（参数为 CommentRow）。</summary>
    public RelayCommand ApplyRowCommand { get; }

    private void OnDataChanged(string kind)
    {
        // DataChanged 在后台线程（帧循环）触发，刷新 ObservableCollection 必须回到 UI 线程
        if (_dispatcher.CheckAccess())
        {
            ApplyRefresh(kind);
        }
        else
        {
            _dispatcher.BeginInvoke(() => ApplyRefresh(kind));
        }
    }

    private void ApplyRefresh(string kind)
    {
        if (kind == "variable")
        {
            RefreshComments();
        }
    }

    private void LoadComment()
    {
        if (!int.TryParse(CommentIndex, out int idx))
        {
            return;
        }
        string name = $"{SelectedCommentType}[{idx}]";
        _controller.Variables.TryGetVariableWords(name, out var words);
        CommentValue = words != null ? ShortsToString(words) : "";
    }

    private void ApplyComment(object? param)
    {
        if (!int.TryParse(CommentIndex, out int idx))
        {
            return;
        }
        string name = $"{SelectedCommentType}[{idx}]";
        var words = StringToShorts(CommentValue);
        _controller.Variables.TrySetVariableWords(name, words);
        RefreshComments();
    }

    /// <summary>从 VariableStore 枚举所有注释变量并按类型（前缀）分组刷新。</summary>
    private void RefreshComments()
    {
        var all = _controller.Variables.GetAllComments();

        // 保持已有分组顺序：先在 UI 中出现的类型排前面，再追加新出现的类型
        var groupMap = CommentGroups.ToDictionary(g => g.TypeName, StringComparer.Ordinal);
        foreach (var entry in all)
        {
            string type = GetTypeName(entry.Name);
            if (!groupMap.TryGetValue(type, out var group))
            {
                group = new CommentGroup(type);
                groupMap[type] = group;
                CommentGroups.Add(group);
            }
            group.Items.Clear();
        }
        // 重新填充各分组（转成可编辑行条目）
        foreach (var entry in all)
        {
            string type = GetTypeName(entry.Name);
            groupMap[type].Items.Add(new CommentRow(entry));
        }
        // 清理空分组
        for (int i = CommentGroups.Count - 1; i >= 0; i--)
        {
            if (CommentGroups[i].Items.Count == 0)
            {
                CommentGroups.RemoveAt(i);
            }
        }
    }

    /// <summary>表格内按行写回注释变量（保存该行编辑后的 Text）。</summary>
    private void ApplyRow(CommentRow? row)
    {
        if (row == null || string.IsNullOrEmpty(row.Name))
        {
            return;
        }
        var words = StringToShorts(row.Text);
        _controller.Variables.TrySetVariableWords(row.Name, words);
        RefreshComments();
    }

    /// <summary>从注释变量名提取类型前缀（如 DI[C1] → DI）。</summary>
    private static string GetTypeName(string name)
    {
        int open = name.IndexOf('[');
        return open > 0 ? name[..open] : name;
    }

    // ---- 工具转换 ----
    private static string ShortsToString(short[] words)
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
        return Encoding.Default.GetString(bytes).TrimEnd('\0');
    }

    private static short[] StringToShorts(string value)
    {
        var bytes = Encoding.Default.GetBytes(value ?? string.Empty);
        var words = new short[40];
        for (int i = 0; i < 40; i++)
        {
            words[i] = (short)((i * 2 < bytes.Length ? bytes[i * 2] : 0)
                             | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
        return words;
    }
}

/// <summary>按类型分组的注释展示项（类型标题 + 该类型下的变量/注释条目）。</summary>
public sealed class CommentGroup
{
    /// <summary>类型前缀，如 DI、R、SR。</summary>
    public string TypeName { get; }

    /// <summary>该类型下的注释条目（可编辑行）。</summary>
    public ObservableCollection<CommentRow> Items { get; } = new();

    public CommentGroup(string typeName)
    {
        TypeName = typeName;
    }
}

/// <summary>表格内可编辑的注释行条目（变量名 + 可编辑注释文本）。</summary>
public sealed class CommentRow : ViewModelBase
{
    /// <summary>原始变量名，如 DI[C1]（写回用）。</summary>
    public string Name { get; }

    /// <summary>展示用变量名，去掉注释索引 C，如 DI[1]。</summary>
    public string DisplayName { get; }

    private string _text;
    /// <summary>注释文本（可在表格中直接编辑）。</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public CommentRow(CommentEntry entry)
    {
        Name = entry.Name;
        DisplayName = entry.DisplayName;
        _text = entry.Text;
    }
}
