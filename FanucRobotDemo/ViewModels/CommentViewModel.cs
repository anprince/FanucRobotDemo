using System.Collections.ObjectModel;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 注释页 ViewModel：注释单条读写 + 批量读取。
/// </summary>
public sealed class CommentViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private int _commentTypeIndex;
    private string _commentIndex = "1";
    private string _commentReadResult = "-";
    private string _commentValue = "";
    private string _commentCount = "10";

    public CommentViewModel(RobotClientService service)
    {
        _service = service;
        ReadCommand = new RelayCommandNoArg(Read);
        WriteCommand = new RelayCommandNoArg(Write);
        BatchReadCommand = new RelayCommandNoArg(BatchRead);
    }

    /// <summary>注释类型前缀列表（与 View 的 ComboBox 顺序一致）。</summary>
    public static readonly string[] Prefixes = { "R", "PR", "SR", "DI", "DO", "RI", "RO", "UI", "UO", "SI", "SO", "GI", "GO", "AI", "AO", "WI", "WO", "F" };

    public int CommentTypeIndex { get => _commentTypeIndex; set => SetProperty(ref _commentTypeIndex, value); }
    public string CommentIndex { get => _commentIndex; set => SetProperty(ref _commentIndex, value); }
    public string CommentReadResult { get => _commentReadResult; set => SetProperty(ref _commentReadResult, value); }
    public string CommentValue { get => _commentValue; set => SetProperty(ref _commentValue, value); }
    public string CommentCount { get => _commentCount; set => SetProperty(ref _commentCount, value); }

    public ObservableCollection<CommentItem> BatchItems { get; } = new();

    public RelayCommandNoArg ReadCommand { get; }
    public RelayCommandNoArg WriteCommand { get; }
    public RelayCommandNoArg BatchReadCommand { get; }

    private string GetPrefix()
    {
        int i = Math.Clamp(CommentTypeIndex, 0, Prefixes.Length - 1);
        return Prefixes[i];
    }

    private async void Read()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(CommentIndex, out var idx)) return;
        string prefix = GetPrefix();
        string addr = $"{prefix}[{idx}]";
        try
        {
            string val = await _service.Robot.Comment.ReadAsync(prefix, idx);
            CommentReadResult = string.IsNullOrEmpty(val) ? "(空注释)" : val;
            CommentValue = val;
            _service.Log($"读取 {addr}  Comment= {(string.IsNullOrEmpty(val) ? "(空注释)" : val)}");
            // DIAG: dump encoding name + char codepoints so we can confirm what bytes were decoded
            var dump = string.Join("", val.Select(c => ((int)c).ToString("X4")));
            _service.Log($"[DIAG] {addr} enc={_service.Robot.StringEncoding.EncodingName}(cp={_service.Robot.StringEncoding.CodePage}) chars=[{dump}]");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 {addr} 失败: {ex.Message}");
        }
    }

    private async void Write()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(CommentIndex, out var idx)) return;
        string prefix = GetPrefix();
        string value = CommentValue ?? string.Empty;
        string addr = $"{prefix}[{idx}]";
        try
        {
            await _service.Robot.Comment.WriteAsync(prefix, idx, value);
            _service.Log($"✅ 写入 {addr}  Comment= {(string.IsNullOrEmpty(value) ? "(空)" : value)} 成功");
            string readBack = await _service.Robot.Comment.ReadAsync(prefix, idx);
            CommentReadResult = string.IsNullOrEmpty(readBack) ? "(空注释)" : readBack;
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 {addr} 失败: {ex.Message}");
        }
    }

    private async void BatchRead()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(CommentIndex, out var idx)) return;
        if (!int.TryParse(CommentCount, out var count) || count < 1) count = 10;
        string prefix = GetPrefix();
        try
        {
            var items = new List<CommentItem>();
            for (int i = 0; i < count; i++)
            {
                int curIdx = idx + i;
                string val = await _service.Robot.Comment.ReadAsync(prefix, curIdx);
                items.Add(new CommentItem { Address = $"{prefix}[{curIdx}]", Comment = string.IsNullOrEmpty(val) ? "(空)" : val });
            }
            BatchItems.Clear();
            foreach (var item in items)
            {
                BatchItems.Add(item);
            }
            _service.Log($"批量读取 {prefix}[{idx}]~{prefix}[{idx + count - 1}] 完成 ({count} 条)");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 批量读取注释失败: {ex.Message}");
        }
    }

    private static bool TryParseIndex(string text, out int index)
    {
        if (int.TryParse(text, out index))
        {
            return true;
        }
        index = 1;
        return true;
    }
}

/// <summary>注释批量读取结果条目。</summary>
public sealed class CommentItem
{
    public string Address { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
