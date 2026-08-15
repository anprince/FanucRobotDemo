using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using FanucRobotInterface.Server.Simulation;

namespace FanucRobotServerSH.ViewModels;

/// <summary>
/// 寄存器/信号编辑页 ViewModel：R/SR/F 数值编辑 + DI/DO/AI/AO 信号查看修改。
/// </summary>
public sealed class RegisterSignalViewModel : ViewModelBase
{
    private readonly SimulatedController _controller;
    private readonly Dispatcher _dispatcher;
    private string _rStart = "1";
    private string _rCount = "10";
    private string _srIndex = "1";
    private string _fStart = "1";
    private string _fCount = "16";

    public RegisterSignalViewModel(SimulatedController controller, Dispatcher dispatcher)
    {
        _controller = controller;
        _dispatcher = dispatcher;
        LoadRCommand = new RelayCommandNoArg(LoadR);
        LoadSrCommand = new RelayCommandNoArg(LoadSr);
        LoadFCommand = new RelayCommandNoArg(LoadF);
        RefreshSignalsCommand = new RelayCommandNoArg(RefreshSignals);
        ApplyRCommand = new RelayCommand(ApplyR);
        ApplySrCommand = new RelayCommand(ApplySr);
        ApplyFCommand = new RelayCommand(ApplyF);
        ToggleDiCommand = new RelayCommand(p => ToggleBit(0, (p as SignalItem)?.Index ?? 0, isInput: true));
        ToggleDoCommand = new RelayCommand(p => ToggleBit(0, (p as SignalItem)?.Index ?? 0, isInput: false));
        SetAnalogCommand = new RelayCommand(SetAnalog);
        controller.DataChanged += OnDataChanged;

        // 默认加载
        LoadR();
        LoadSr();
        LoadF();
        RefreshSignals();
    }

    public string RStart { get => _rStart; set => SetProperty(ref _rStart, value); }
    public string RCount { get => _rCount; set => SetProperty(ref _rCount, value); }
    public string SrIndex { get => _srIndex; set => SetProperty(ref _srIndex, value); }
    public string FStart { get => _fStart; set => SetProperty(ref _fStart, value); }
    public string FCount { get => _fCount; set => SetProperty(ref _fCount, value); }

    public ObservableCollection<RegItem> RItems { get; } = new();
    public ObservableCollection<RegItem> SrItems { get; } = new();
    public ObservableCollection<FlagItem> FItems { get; } = new();
    public ObservableCollection<SignalItem> DiItems { get; } = new();
    public ObservableCollection<SignalItem> DoItems { get; } = new();
    public ObservableCollection<AnalogItem> AiItems { get; } = new();
    public ObservableCollection<AnalogItem> AoItems { get; } = new();

    public RelayCommandNoArg LoadRCommand { get; }
    public RelayCommandNoArg LoadSrCommand { get; }
    public RelayCommandNoArg LoadFCommand { get; }
    public RelayCommandNoArg RefreshSignalsCommand { get; }
    public RelayCommand ApplyRCommand { get; }
    public RelayCommand ApplySrCommand { get; }
    public RelayCommand ApplyFCommand { get; }
    public RelayCommand ToggleDiCommand { get; }
    public RelayCommand ToggleDoCommand { get; }
    public RelayCommand SetAnalogCommand { get; }

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
            LoadR();
            LoadSr();
            LoadF();
        }
        else if (kind == "signal")
        {
            RefreshSignals();
        }
    }

    // ---- R 数值寄存器（词表 float，2 字/值）----
    private void LoadR()
    {
        if (!int.TryParse(RStart, out int start) || !int.TryParse(RCount, out int count) || count < 1 || count > 100)
        {
            return;
        }
        RItems.Clear();
        for (int i = 0; i < count; i++)
        {
            int idx = start + i;
            var words = ReadVariableWords($"R[{idx}]");
            float val = words != null ? ShortsToFloat(words) : 0f;
            RItems.Add(new RegItem(idx, val));
        }
    }

    // ---- SR 字符串寄存器（40 字/值）----
    private void LoadSr()
    {
        if (!int.TryParse(SrIndex, out int idx))
        {
            return;
        }
        SrItems.Clear();
        var words = ReadVariableWords($"SR[{idx}]");
        string text = words != null ? ShortsToString(words) : "";
        SrItems.Add(new RegItem(idx, text));
    }

    // ---- F 标志寄存器（1 字 bool）----
    private void LoadF()
    {
        if (!int.TryParse(FStart, out int start) || !int.TryParse(FCount, out int count) || count < 1 || count > 64)
        {
            return;
        }
        FItems.Clear();
        for (int i = 0; i < count; i++)
        {
            int idx = start + i;
            var words = ReadVariableWords($"F[{idx}]");
            bool on = words != null && words.Length > 0 && words[0] != 0;
            FItems.Add(new FlagItem(idx, on));
        }
    }

    private short[]? ReadVariableWords(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        return words;
    }

    private void RefreshSignals()
    {
        // 位信号：真实控制器中 DI（输入区 I）与 DO（输出区 Q）相互独立，地址偏移均为 0 但分区不同。
        // DI 走读 selector 72、DO 走写 selector 70，分别映射到输入区与输出区。
        DiItems.Clear();
        DoItems.Clear();
        for (int i = 1; i <= 32; i++)
        {
            DiItems.Add(new SignalItem(i, _controller.GetBit(0, i, isInput: true)));
            DoItems.Add(new SignalItem(i, _controller.GetBit(0, i, isInput: false)));
        }

        // 模拟信号：AI（输入区 I）与 AO（输出区 Q）相互独立（baseOffset=1000，分区不同）。
        AiItems.Clear();
        AoItems.Clear();
        for (int i = 1; i <= 8; i++)
        {
            AiItems.Add(new AnalogItem(i, _controller.Signals.GetAnalogByOffset(1000, i, isInput: true)));
            AoItems.Add(new AnalogItem(i, _controller.Signals.GetAnalogByOffset(1000, i, isInput: false)));
        }
    }

    // ---- 应用编辑 ----
    private void ApplyR(object? param)
    {
        if (param is not RegItem item || !float.TryParse(item.Value, out float val))
        {
            return;
        }
        var words = FloatToShorts(val);
        _controller.Variables.TrySetVariableWords($"R[{item.Index}]", words);
        OnDataChanged("variable");
    }

    private void ApplySr(object? param)
    {
        if (param is not RegItem item)
        {
            return;
        }
        var words = StringToShorts(item.Value);
        _controller.Variables.TrySetVariableWords($"SR[{item.Index}]", words);
        OnDataChanged("variable");
    }

    private void ApplyF(object? param)
    {
        if (param is not FlagItem item)
        {
            return;
        }
        _controller.Variables.TrySetVariableWords($"F[{item.Index}]", new[] { (short)(item.IsOn ? 1 : 0) });
        OnDataChanged("variable");
    }

    private void ToggleBit(int baseOffset, int index, bool isInput)
    {
        if (index <= 0)
        {
            return;
        }
        bool newVal = !_controller.GetBit(baseOffset, index, isInput);
        _controller.SetBit(baseOffset, index, newVal, isInput);
        OnDataChanged("signal");
    }

    private void SetAnalog(object? param)
    {
        if (param is not AnalogItem item)
        {
            return;
        }
        _controller.Signals.SetAnalogByOffset(1000, item.Index, item.Value, isInput: false);
        OnDataChanged("signal");
    }

    // ---- 工具转换 ----
    private static float ShortsToFloat(short[] words)
    {
        if (words.Length < 2)
        {
            return 0f;
        }
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), words[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), words[1]);
        return BitConverter.ToSingle(bytes, 0);
    }

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

    private static short[] FloatToShorts(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new[]
        {
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2))
        };
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

/// <summary>数值/字符串寄存器条目。</summary>
public sealed class RegItem : ViewModelBase
{
    private string _value;

    public int Index { get; }

    public string IndexText => $"R[{Index}]";

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public RegItem(int index, string value)
    {
        Index = index;
        _value = value;
    }

    public RegItem(int index, float value)
    {
        Index = index;
        _value = value.ToString("0.###");
    }
}

/// <summary>标志寄存器条目。</summary>
public sealed class FlagItem : ViewModelBase
{
    private bool _isOn;

    public int Index { get; }
    public string IndexText => $"F[{Index}]";

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    public FlagItem(int index, bool isOn)
    {
        Index = index;
        _isOn = isOn;
    }
}

/// <summary>数字信号条目。</summary>
public sealed class SignalItem : ViewModelBase
{
    private bool _isOn;

    public int Index { get; }
    public string IndexText => $"#{Index}";

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    public SignalItem(int index, bool isOn)
    {
        Index = index;
        _isOn = isOn;
    }
}

/// <summary>模拟信号条目。</summary>
public sealed class AnalogItem : ViewModelBase
{
    private int _value;

    public int Index { get; }
    public string IndexText => $"#{Index}";

    public int Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public AnalogItem(int index, int value)
    {
        Index = index;
        _value = value;
    }
}
