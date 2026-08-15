namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 寄存器页 ViewModel：R/SR/F 读写 + R 批量读写 + SR 批量读。
/// </summary>
public sealed class RegisterViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _rIndex = "1";
    private string _rResult = "-";
    private string _rWriteIndex = "1";
    private string _rWriteValue = "3.14";
    private string _rBatchResult = "-";

    private string _srIndex = "1";
    private string _srResult = "-";
    private string _srWriteIndex = "1";
    private string _srWriteValue = "";

    private string _fIndex = "1";
    private string _fResult = "-";
    private string _fWriteIndex = "1";
    private int _fValueIndex;

    private string _rWriteBatchStart = "1";
    private string _rWriteBatchValues = "1.0, 2.0, 3.0, 4.0, 5.0";

    private string _srBatchStart = "1";
    private string _srBatchCount = "5";
    private string _srBatchResult = "-";

    public RegisterViewModel(RobotClientService service)
    {
        _service = service;
        ReadRCommand = new RelayCommandNoArg(ReadR);
        WriteRCommand = new RelayCommandNoArg(WriteR);
        ReadRBatchCommand = new RelayCommandNoArg(ReadRBatch);
        ReadSrCommand = new RelayCommandNoArg(ReadSr);
        WriteSrCommand = new RelayCommandNoArg(WriteSr);
        ReadFCommand = new RelayCommandNoArg(ReadF);
        WriteFCommand = new RelayCommandNoArg(WriteF);
        WriteRBatchCommand = new RelayCommandNoArg(WriteRBatch);
        ReadSrBatchCommand = new RelayCommandNoArg(ReadSrBatch);
    }

    public string RIndex { get => _rIndex; set => SetProperty(ref _rIndex, value); }
    public string RResult { get => _rResult; set => SetProperty(ref _rResult, value); }
    public string RWriteIndex { get => _rWriteIndex; set => SetProperty(ref _rWriteIndex, value); }
    public string RWriteValue { get => _rWriteValue; set => SetProperty(ref _rWriteValue, value); }
    public string RBatchResult { get => _rBatchResult; set => SetProperty(ref _rBatchResult, value); }

    public string SrIndex { get => _srIndex; set => SetProperty(ref _srIndex, value); }
    public string SrResult { get => _srResult; set => SetProperty(ref _srResult, value); }
    public string SrWriteIndex { get => _srWriteIndex; set => SetProperty(ref _srWriteIndex, value); }
    public string SrWriteValue { get => _srWriteValue; set => SetProperty(ref _srWriteValue, value); }

    public string FIndex { get => _fIndex; set => SetProperty(ref _fIndex, value); }
    public string FResult { get => _fResult; set => SetProperty(ref _fResult, value); }
    public string FWriteIndex { get => _fWriteIndex; set => SetProperty(ref _fWriteIndex, value); }
    public int FValueIndex { get => _fValueIndex; set => SetProperty(ref _fValueIndex, value); }

    public string RWriteBatchStart { get => _rWriteBatchStart; set => SetProperty(ref _rWriteBatchStart, value); }
    public string RWriteBatchValues { get => _rWriteBatchValues; set => SetProperty(ref _rWriteBatchValues, value); }

    public string SrBatchStart { get => _srBatchStart; set => SetProperty(ref _srBatchStart, value); }
    public string SrBatchCount { get => _srBatchCount; set => SetProperty(ref _srBatchCount, value); }
    public string SrBatchResult { get => _srBatchResult; set => SetProperty(ref _srBatchResult, value); }

    public RelayCommandNoArg ReadRCommand { get; }
    public RelayCommandNoArg WriteRCommand { get; }
    public RelayCommandNoArg ReadRBatchCommand { get; }
    public RelayCommandNoArg ReadSrCommand { get; }
    public RelayCommandNoArg WriteSrCommand { get; }
    public RelayCommandNoArg ReadFCommand { get; }
    public RelayCommandNoArg WriteFCommand { get; }
    public RelayCommandNoArg WriteRBatchCommand { get; }
    public RelayCommandNoArg ReadSrBatchCommand { get; }

    private async void ReadR()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(RIndex, out var idx)) return;
        try
        {
            float val = await _service.Robot.NumReg.ReadAsync(idx);
            RResult = val.ToString("F3");
            _service.Log($"R[{idx}] = {val:F3}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 R[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteR()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(RWriteIndex, out var idx)) return;
        if (!float.TryParse(RWriteValue, out var val)) return;
        try
        {
            await _service.Robot.NumReg.WriteAsync(idx, val);
            _service.Log($"✅ R[{idx}] = {val} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 R[{idx}] 失败: {ex.Message}");
        }
    }

    private async void ReadRBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            float[] vals = await _service.Robot.NumReg.ReadBatchAsync(1, 10);
            RBatchResult = string.Join(", ", vals.Select((v, i) => $"R[{i + 1}]={v:F3}"));
            _service.Log("批量读取 R[1]~R[10] 完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 批量读取失败: {ex.Message}");
        }
    }

    private async void ReadSr()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(SrIndex, out var idx)) return;
        try
        {
            string val = await _service.Robot.StrReg.ReadAsync(idx);
            SrResult = string.IsNullOrEmpty(val) ? "(空字符串)" : val;
            _service.Log($"SR[{idx}] = \"{val}\"");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 SR[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteSr()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(SrWriteIndex, out var idx)) return;
        string value = SrWriteValue ?? string.Empty;
        try
        {
            await _service.Robot.StrReg.WriteAsync(idx, value);
            _service.Log($"✅ SR[{idx}] = \"{value}\" 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 SR[{idx}] 失败: {ex.Message}");
        }
    }

    private async void ReadF()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(FIndex, out var idx)) return;
        try
        {
            bool val = await _service.Robot.Flag.ReadAsync(idx);
            FResult = val ? "ON 🔴" : "OFF ⚪";
            _service.Log($"F[{idx}] = {(val ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 F[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteF()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(FWriteIndex, out var idx)) return;
        bool val = FValueIndex == 0;
        try
        {
            await _service.Robot.Flag.WriteAsync(idx, val);
            _service.Log($"✅ F[{idx}] = {(val ? "ON" : "OFF")} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 F[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteRBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(RWriteBatchStart, out var start)) start = 1;

        var values = new List<float>();
        foreach (var p in RWriteBatchValues.Split(','))
        {
            if (float.TryParse(p.Trim(), out var v)) values.Add(v);
        }
        if (values.Count == 0) return;
        try
        {
            await _service.Robot.NumReg.WriteBatchAsync(start, values.ToArray());
            _service.Log($"✅ R[{start}]~R[{start + values.Count - 1}] 批量写入 {values.Count} 个值成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ R 批量写入失败: {ex.Message}");
        }
    }

    private async void ReadSrBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(SrBatchStart, out var start)) start = 1;
        if (!int.TryParse(SrBatchCount, out var count) || count < 1) count = 5;
        try
        {
            string[] vals = await _service.Robot.StrReg.ReadBatchAsync(start, count);
            SrBatchResult = string.Join(" | ", vals.Select((v, i) => $"SR[{start + i}]=\"{v}\""));
            _service.Log($"批量读取 SR[{start}]~SR[{start + count - 1}] 完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ SR 批量读取失败: {ex.Message}");
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
