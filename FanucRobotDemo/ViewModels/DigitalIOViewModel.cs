using FanucRobotInterface.Common.Signals;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 数字 I/O 页 ViewModel：DI/DO 单路读写 + DO 批量 + 扩展数字信号 + 组信号 + PMC + AI/AO。
/// </summary>
public sealed class DigitalIOViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _diIndex = "1";
    private string _diResult = "-";
    private string _doIndex = "1";
    private int _doValueIndex;
    private string _doResult = "-";
    private string _doBatchSummary = "";
    private string _aiResult = "-";
    private string _aoValue = "500";

    private int _extDigTypeIndex;
    private string _extDigIndex = "1";
    private string _extDigResult = "-";

    private string _giIndex = "1";
    private string _giResult = "-";
    private string _goIndex = "1";
    private string _goValue = "255";
    private string _goResult = "-";

    private int _pmcZoneIndex;
    private string _pmcIndex = "1";
    private string _pmcResult = "-";
    private string _pmcDataIndex = "1";
    private string _pmcDataValue = "0";
    private string _pmcDataResult = "-";

    public DigitalIOViewModel(RobotClientService service)
    {
        _service = service;

        ReadDiCommand = new RelayCommandNoArg(ReadDi);
        ReadDoCommand = new RelayCommandNoArg(ReadDo);
        WriteDoCommand = new RelayCommandNoArg(WriteDo);
        ReadDoBatchCommand = new RelayCommandNoArg(ReadDoBatch);
        ReadAiCommand = new RelayCommandNoArg(ReadAi);
        WriteAoCommand = new RelayCommandNoArg(WriteAo);

        ReadExtDigCommand = new RelayCommandNoArg(ReadExtDig);
        WriteExtDigCommand = new RelayCommand(p => WriteExtDig((string?)p == "ON"));

        ReadGiCommand = new RelayCommandNoArg(ReadGi);
        ReadGoCommand = new RelayCommandNoArg(ReadGo);
        WriteGoCommand = new RelayCommandNoArg(WriteGo);

        ReadPmcCommand = new RelayCommandNoArg(ReadPmc);
        WritePmcCommand = new RelayCommand(p => WritePmc((string?)p == "ON"));
        ReadPmcDataCommand = new RelayCommandNoArg(ReadPmcData);
        WritePmcDataCommand = new RelayCommandNoArg(WritePmcData);
    }

    // ---- 属性 ----
    public string DiIndex { get => _diIndex; set => SetProperty(ref _diIndex, value); }
    public string DiResult { get => _diResult; set => SetProperty(ref _diResult, value); }

    public string DoIndex { get => _doIndex; set => SetProperty(ref _doIndex, value); }
    public int DoValueIndex { get => _doValueIndex; set => SetProperty(ref _doValueIndex, value); }
    public string DoResult { get => _doResult; set => SetProperty(ref _doResult, value); }
    public string DoBatchSummary { get => _doBatchSummary; set => SetProperty(ref _doBatchSummary, value); }

    public string AiResult { get => _aiResult; set => SetProperty(ref _aiResult, value); }
    public string AoValue { get => _aoValue; set => SetProperty(ref _aoValue, value); }

    public int ExtDigTypeIndex { get => _extDigTypeIndex; set => SetProperty(ref _extDigTypeIndex, value); }
    public string ExtDigIndex { get => _extDigIndex; set => SetProperty(ref _extDigIndex, value); }
    public string ExtDigResult { get => _extDigResult; set => SetProperty(ref _extDigResult, value); }

    public string GiIndex { get => _giIndex; set => SetProperty(ref _giIndex, value); }
    public string GiResult { get => _giResult; set => SetProperty(ref _giResult, value); }
    public string GoIndex { get => _goIndex; set => SetProperty(ref _goIndex, value); }
    public string GoValue { get => _goValue; set => SetProperty(ref _goValue, value); }
    public string GoResult { get => _goResult; set => SetProperty(ref _goResult, value); }

    public int PmcZoneIndex { get => _pmcZoneIndex; set => SetProperty(ref _pmcZoneIndex, value); }
    public string PmcIndex { get => _pmcIndex; set => SetProperty(ref _pmcIndex, value); }
    public string PmcResult { get => _pmcResult; set => SetProperty(ref _pmcResult, value); }
    public string PmcDataIndex { get => _pmcDataIndex; set => SetProperty(ref _pmcDataIndex, value); }
    public string PmcDataValue { get => _pmcDataValue; set => SetProperty(ref _pmcDataValue, value); }
    public string PmcDataResult { get => _pmcDataResult; set => SetProperty(ref _pmcDataResult, value); }

    // ---- 命令 ----
    public RelayCommandNoArg ReadDiCommand { get; }
    public RelayCommandNoArg ReadDoCommand { get; }
    public RelayCommandNoArg WriteDoCommand { get; }
    public RelayCommandNoArg ReadDoBatchCommand { get; }
    public RelayCommandNoArg ReadAiCommand { get; }
    public RelayCommandNoArg WriteAoCommand { get; }
    public RelayCommandNoArg ReadExtDigCommand { get; }
    public RelayCommand WriteExtDigCommand { get; }
    public RelayCommandNoArg ReadGiCommand { get; }
    public RelayCommandNoArg ReadGoCommand { get; }
    public RelayCommandNoArg WriteGoCommand { get; }
    public RelayCommandNoArg ReadPmcCommand { get; }
    public RelayCommand WritePmcCommand { get; }
    public RelayCommandNoArg ReadPmcDataCommand { get; }
    public RelayCommandNoArg WritePmcDataCommand { get; }

    // ---- DI ----
    private async void ReadDi()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(DiIndex, out var idx)) return;
        try
        {
            bool val = await _service.Robot.DI.ReadSingleAsync(idx);
            DiResult = val ? "ON 🔵" : "OFF ⚪";
            _service.Log($"DI[{idx}] = {(val ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 DI[{idx}] 失败: {ex.Message}");
        }
    }

    // ---- DO ----
    private async void ReadDo()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(DoIndex, out var idx)) return;
        try
        {
            bool val = await _service.Robot.DO.ReadSingleAsync(idx);
            DoResult = val ? "ON 🟠" : "OFF ⚪";
            _service.Log($"DO[{idx}] = {(val ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 DO[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteDo()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(DoIndex, out var idx)) return;
        bool val = DoValueIndex == 0;
        try
        {
            await _service.Robot.DO.WriteSingleAsync(idx, val);
            _service.Log($"✅ DO[{idx}] = {(val ? "ON" : "OFF")} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 DO[{idx}] 失败: {ex.Message}");
        }
    }

    private async void ReadDoBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            bool[] vals = await _service.Robot.DO.ReadAsync(1, 8);
            DoBatchSummary = string.Join(", ", vals.Select((v, i) => $"DO[{i + 1}]={(v ? "ON" : "OFF")}"));
            _service.Log($"批量读取 DO[1]~DO[8]: [{string.Join(", ", vals.Select(v => v ? "ON" : "OFF"))}]");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 批量读取失败: {ex.Message}");
        }
    }

    // ---- AI/AO ----
    private async void ReadAi()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            int val = await _service.Robot.AI.ReadSingleAsync(1);
            AiResult = val.ToString();
            _service.Log($"AI[1] = {val}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 AI 失败: {ex.Message}");
        }
    }

    private async void WriteAo()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(AoValue, out var val)) return;
        try
        {
            await _service.Robot.AO.WriteSingleAsync(1, val);
            _service.Log($"✅ AO[1] = {val} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 AO 失败: {ex.Message}");
        }
    }

    // ---- 扩展数字信号 ----
    private SignalBase<bool> GetExtDigitalSignal(string type)
    {
        var robot = _service.Robot!;
        return type switch
        {
            "RI" => robot.RI,
            "RO" => robot.RO,
            "UI" => robot.UI,
            "UO" => robot.UO,
            "SI" => robot.SI,
            "SO" => robot.SO,
            "WI" => robot.WI,
            "WO" => robot.WO,
            "WSI" => robot.WSI,
            "WSO" => robot.WSO,
            _ => throw new ArgumentException($"未知数字信号类型: {type}")
        };
    }

    private static readonly string[] ExtDigTypes = { "RI", "RO", "UI", "UO", "SI", "SO", "WI", "WO", "WSI", "WSO" };

    private async void ReadExtDig()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        string type = ExtDigTypes[Math.Clamp(ExtDigTypeIndex, 0, ExtDigTypes.Length - 1)];
        if (!int.TryParse(ExtDigIndex, out var idx)) idx = 1;
        try
        {
            bool val = await GetExtDigitalSignal(type).ReadSingleAsync(idx);
            ExtDigResult = val ? "ON" : "OFF";
            _service.Log($"{type}[{idx}] = {(val ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 {type}[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteExtDig(bool val)
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        string type = ExtDigTypes[Math.Clamp(ExtDigTypeIndex, 0, ExtDigTypes.Length - 1)];
        if (!int.TryParse(ExtDigIndex, out var idx)) idx = 1;
        try
        {
            await GetExtDigitalSignal(type).WriteSingleAsync(idx, val);
            _service.Log($"✅ {type}[{idx}] = {(val ? "ON" : "OFF")} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 {type}[{idx}] 失败: {ex.Message}");
        }
    }

    // ---- 组信号 ----
    private async void ReadGi()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(GiIndex, out var idx)) idx = 1;
        try
        {
            int val = await _service.Robot.GI.ReadSingleAsync(idx);
            GiResult = val.ToString();
            _service.Log($"GI[{idx}] = {val} (0x{val:X4})");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 GI[{idx}] 失败: {ex.Message}");
        }
    }

    private async void ReadGo()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(GoIndex, out var idx)) idx = 1;
        try
        {
            int val = await _service.Robot.GO.ReadSingleAsync(idx);
            GoResult = val.ToString();
            _service.Log($"GO[{idx}] = {val} (0x{val:X4})");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 GO[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WriteGo()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(GoIndex, out var idx)) idx = 1;
        if (!int.TryParse(GoValue, out var val)) return;
        try
        {
            await _service.Robot.GO.WriteSingleAsync(idx, val);
            _service.Log($"✅ GO[{idx}] = {val} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 GO[{idx}] 失败: {ex.Message}");
        }
    }

    // ---- PMC ----
    private async void ReadPmc()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        bool isKeep = PmcZoneIndex == 1;
        if (!int.TryParse(PmcIndex, out var idx)) idx = 1;
        try
        {
            bool val = isKeep
                ? await _service.Robot.Pmc.ReadKeepAsync(idx)
                : await _service.Robot.Pmc.ReadRelayAsync(idx);
            PmcResult = val ? "ON" : "OFF";
            _service.Log($"PMC {(isKeep ? "K" : "R")}[{idx}] = {(val ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 PMC 失败: {ex.Message}");
        }
    }

    private async void WritePmc(bool val)
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        bool isKeep = PmcZoneIndex == 1;
        if (!int.TryParse(PmcIndex, out var idx)) idx = 1;
        try
        {
            if (isKeep)
                await _service.Robot.Pmc.WriteKeepAsync(idx, val);
            else
                await _service.Robot.Pmc.WriteRelayAsync(idx, val);
            _service.Log($"✅ PMC {(isKeep ? "K" : "R")}[{idx}] = {(val ? "ON" : "OFF")} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 PMC 失败: {ex.Message}");
        }
    }

    private async void ReadPmcData()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PmcDataIndex, out var idx)) idx = 1;
        try
        {
            int val = await _service.Robot.Pmc.ReadDataAsync(idx);
            PmcDataResult = val.ToString();
            _service.Log($"PMC D[{idx}] = {val}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 PMC D[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WritePmcData()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PmcDataIndex, out var idx)) idx = 1;
        if (!int.TryParse(PmcDataValue, out var val)) return;
        try
        {
            await _service.Robot.Pmc.WriteDataAsync(idx, val);
            _service.Log($"✅ PMC D[{idx}] = {val} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 PMC D[{idx}] 失败: {ex.Message}");
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
