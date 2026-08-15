using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using FanucRobotInterface.Common.Data;
using FanucRobotInterface.Server.Simulation;

namespace FanucRobotServerSH.ViewModels;

/// <summary>
/// 位置/系统变量页 ViewModel：当前位姿 POS、位置寄存器 PR、常用系统变量查看编辑。
/// </summary>
public sealed class PositionSysvarViewModel : ViewModelBase
{
    private readonly SimulatedController _controller;
    private readonly Dispatcher _dispatcher;
    private string _prIndex = "1";
    private string _sysvarName = "$SYSNAME";

    // 当前位姿（POS[1]）字段
    private string _posX = "0", _posY = "0", _posZ = "0", _posW = "0", _posP = "0", _posR = "0";
    private string _posJ1 = "0", _posJ2 = "0", _posJ3 = "0", _posJ4 = "0", _posJ5 = "0", _posJ6 = "0";
    private string _sysvarValue = "";
    private SysvarType _sysvarType = SysvarType.STRING;

    public PositionSysvarViewModel(SimulatedController controller, Dispatcher dispatcher)
    {
        _controller = controller;
        _dispatcher = dispatcher;
        LoadPosCommand = new RelayCommandNoArg(LoadPos);
        ApplyPosCommand = new RelayCommandNoArg(ApplyPos);
        LoadPrCommand = new RelayCommandNoArg(LoadPr);
        ApplyPrCommand = new RelayCommandNoArg(ApplyPr);
        ReadSysvarCommand = new RelayCommandNoArg(ReadSysvar);
        WriteSysvarCommand = new RelayCommandNoArg(WriteSysvar);
        controller.DataChanged += OnDataChanged;

        LoadPos();
        LoadPr();
    }

    public string PrIndex { get => _prIndex; set => SetProperty(ref _prIndex, value); }
    public string SysvarName { get => _sysvarName; set => SetProperty(ref _sysvarName, value); }

    // 笛卡尔
    public string PosX { get => _posX; set => SetProperty(ref _posX, value); }
    public string PosY { get => _posY; set => SetProperty(ref _posY, value); }
    public string PosZ { get => _posZ; set => SetProperty(ref _posZ, value); }
    public string PosW { get => _posW; set => SetProperty(ref _posW, value); }
    public string PosP { get => _posP; set => SetProperty(ref _posP, value); }
    public string PosR { get => _posR; set => SetProperty(ref _posR, value); }

    // 关节
    public string PosJ1 { get => _posJ1; set => SetProperty(ref _posJ1, value); }
    public string PosJ2 { get => _posJ2; set => SetProperty(ref _posJ2, value); }
    public string PosJ3 { get => _posJ3; set => SetProperty(ref _posJ3, value); }
    public string PosJ4 { get => _posJ4; set => SetProperty(ref _posJ4, value); }
    public string PosJ5 { get => _posJ5; set => SetProperty(ref _posJ5, value); }
    public string PosJ6 { get => _posJ6; set => SetProperty(ref _posJ6, value); }

    public string SysvarValue { get => _sysvarValue; set => SetProperty(ref _sysvarValue, value); }

    /// <summary>系统变量类型（BOOL/INT/REAL/STRING/POS），用于按类型读写。</summary>
    public SysvarType SysvarType
    {
        get => _sysvarType;
        set => SetProperty(ref _sysvarType, value);
    }

    /// <summary>类型下拉框数据源。</summary>
    public SysvarType[] SysvarTypes { get; } = { SysvarType.BOOL, SysvarType.INT, SysvarType.REAL, SysvarType.STRING, SysvarType.POS };

    public RelayCommandNoArg LoadPosCommand { get; }
    public RelayCommandNoArg ApplyPosCommand { get; }
    public RelayCommandNoArg LoadPrCommand { get; }
    public RelayCommandNoArg ApplyPrCommand { get; }
    public RelayCommandNoArg ReadSysvarCommand { get; }
    public RelayCommandNoArg WriteSysvarCommand { get; }

    /// <summary>系统变量显示项（用于读取结果）。</summary>
    public ObservableCollection<SysvarItem> SysvarResults { get; } = new();

    private void OnDataChanged(string kind)
    {
        if (kind != "variable" && kind != "binding")
        {
            return;
        }
        // DataChanged 在后台线程触发，刷新 UI 绑定属性必须回到 UI 线程
        if (_dispatcher.CheckAccess())
        {
            ApplyRefresh();
        }
        else
        {
            _dispatcher.BeginInvoke(ApplyRefresh);
        }
    }

    private void ApplyRefresh()
    {
        LoadPos();
        LoadPr();
    }

    private PositionInfo? ReadPosVar(string name)
    {
        var words = _controller.Positions.Read(name);
        return PositionStore.Parse(words);
    }

    private void LoadPos()
    {
        var p = ReadPosVar("POS[1]");
        if (p == null)
        {
            return;
        }
        PosX = p.Cartesian.X.ToString("0.###");
        PosY = p.Cartesian.Y.ToString("0.###");
        PosZ = p.Cartesian.Z.ToString("0.###");
        PosW = p.Cartesian.W.ToString("0.###");
        PosP = p.Cartesian.P.ToString("0.###");
        PosR = p.Cartesian.R.ToString("0.###");
        PosJ1 = p.Joint.J1.ToString("0.###");
        PosJ2 = p.Joint.J2.ToString("0.###");
        PosJ3 = p.Joint.J3.ToString("0.###");
        PosJ4 = p.Joint.J4.ToString("0.###");
        PosJ5 = p.Joint.J5.ToString("0.###");
        PosJ6 = p.Joint.J6.ToString("0.###");
    }

    private void ApplyPos()
    {
        var p = ReadPosVar("POS[1]") ?? new PositionInfo { ValidCartesian = 1, ValidJoint = 1, UF = 1, UT = 1 };
        p.Cartesian.X = ParseF(PosX);
        p.Cartesian.Y = ParseF(PosY);
        p.Cartesian.Z = ParseF(PosZ);
        p.Cartesian.W = ParseF(PosW);
        p.Cartesian.P = ParseF(PosP);
        p.Cartesian.R = ParseF(PosR);
        p.Joint.J1 = ParseF(PosJ1);
        p.Joint.J2 = ParseF(PosJ2);
        p.Joint.J3 = ParseF(PosJ3);
        p.Joint.J4 = ParseF(PosJ4);
        p.Joint.J5 = ParseF(PosJ5);
        p.Joint.J6 = ParseF(PosJ6);
        _controller.Positions.Write("POS[1]", PositionStore.ToWords(p));
    }

    private void LoadPr()
    {
        if (!int.TryParse(PrIndex, out int idx))
        {
            return;
        }
        var p = ReadPosVar($"PR[{idx}]");
        if (p == null)
        {
            return;
        }
        PosX = p.Cartesian.X.ToString("0.###");
        PosY = p.Cartesian.Y.ToString("0.###");
        PosZ = p.Cartesian.Z.ToString("0.###");
        PosW = p.Cartesian.W.ToString("0.###");
        PosP = p.Cartesian.P.ToString("0.###");
        PosR = p.Cartesian.R.ToString("0.###");
        PosJ1 = p.Joint.J1.ToString("0.###");
        PosJ2 = p.Joint.J2.ToString("0.###");
        PosJ3 = p.Joint.J3.ToString("0.###");
        PosJ4 = p.Joint.J4.ToString("0.###");
        PosJ5 = p.Joint.J5.ToString("0.###");
        PosJ6 = p.Joint.J6.ToString("0.###");
    }

    private void ApplyPr()
    {
        if (!int.TryParse(PrIndex, out int idx))
        {
            return;
        }
        var p = ReadPosVar($"PR[{idx}]") ?? new PositionInfo { ValidCartesian = 1, ValidJoint = 1, UF = 1, UT = 1 };
        p.Cartesian.X = ParseF(PosX);
        p.Cartesian.Y = ParseF(PosY);
        p.Cartesian.Z = ParseF(PosZ);
        p.Cartesian.W = ParseF(PosW);
        p.Cartesian.P = ParseF(PosP);
        p.Cartesian.R = ParseF(PosR);
        p.Joint.J1 = ParseF(PosJ1);
        p.Joint.J2 = ParseF(PosJ2);
        p.Joint.J3 = ParseF(PosJ3);
        p.Joint.J4 = ParseF(PosJ4);
        p.Joint.J5 = ParseF(PosJ5);
        p.Joint.J6 = ParseF(PosJ6);
        _controller.Positions.Write($"PR[{idx}]", PositionStore.ToWords(p));
    }

    private void ReadSysvar()
    {
        string name = SysvarName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        // 按选定类型解码变量词表
        string display = SysvarType switch
        {
            SysvarType.POS => ReadSysvarAsPos(name),
            SysvarType.BOOL => ReadSysvarAsBool(name),
            SysvarType.INT => ReadSysvarAsInt(name),
            SysvarType.REAL => ReadSysvarAsReal(name),
            _ => ReadSysvarAsString(name)
        };
        SysvarResults.Clear();
        SysvarResults.Add(new SysvarItem(name, SysvarType.ToString(), display));
        SysvarValue = display;
    }

    private void WriteSysvar()
    {
        string name = SysvarName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        short[] words;
        try
        {
            words = SysvarType switch
            {
                SysvarType.BOOL => new short[] { (short)(bool.Parse(SysvarValue) ? 1 : 0), 0 },
                SysvarType.INT => int.TryParse(SysvarValue, out int iv) ? new short[] { (short)(iv & 0xFFFF), (short)((iv >> 16) & 0xFFFF) } : new short[2],
                SysvarType.REAL => float.TryParse(SysvarValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv) ? FloatToShorts(fv) : new short[2],
                SysvarType.POS => PositionSysvarToWords(),
                _ => StringToShorts(SysvarValue)
            };
        }
        catch (Exception ex)
        {
            SysvarResults.Clear();
            SysvarResults.Add(new SysvarItem(name, SysvarType.ToString(), $"解析失败: {ex.Message}"));
            return;
        }
        _controller.Variables.TrySetVariableWords(name, words);
        SysvarResults.Clear();
        SysvarResults.Add(new SysvarItem(name, SysvarType.ToString(), SysvarValue));
    }

    // ---- 按类型读取辅助 ----
    private string ReadSysvarAsPos(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        words ??= _controller.Positions.Read(name);
        var p = PositionStore.Parse(words);
        return $"X={p.Cartesian.X:0.###} Y={p.Cartesian.Y:0.###} Z={p.Cartesian.Z:0.###} " +
               $"W={p.Cartesian.W:0.###} P={p.Cartesian.P:0.###} R={p.Cartesian.R:0.###} | " +
               $"J1={p.Joint.J1:0.###} J2={p.Joint.J2:0.###} J3={p.Joint.J3:0.###} " +
               $"J4={p.Joint.J4:0.###} J5={p.Joint.J5:0.###} J6={p.Joint.J6:0.###}";
    }

    private string ReadSysvarAsBool(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        bool v = words != null && words.Length > 0 && words[0] != 0;
        return v ? "ON" : "OFF";
    }

    private string ReadSysvarAsInt(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        if (words == null || words.Length < 2)
        {
            return "0";
        }
        int v = (words[1] << 16) | (words[0] & 0xFFFF);
        return v.ToString();
    }

    private string ReadSysvarAsReal(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        if (words == null || words.Length < 2)
        {
            return "0";
        }
        float v = FloatToString(words);
        return v.ToString("0.###");
    }

    private string ReadSysvarAsString(string name)
    {
        _controller.Variables.TryGetVariableWords(name, out var words);
        words ??= new short[40];
        return ShortsToString(words);
    }

    // ---- POS 写入（从当前 PosX/Y/Z/W/P/R/J1..J6 字段组装） ----
    private short[] PositionSysvarToWords()
    {
        var p = new FanucRobotInterface.Common.Data.PositionInfo { ValidCartesian = 1, ValidJoint = 1, UF = 1, UT = 1 };
        p.Cartesian.X = ParseF(PosX);
        p.Cartesian.Y = ParseF(PosY);
        p.Cartesian.Z = ParseF(PosZ);
        p.Cartesian.W = ParseF(PosW);
        p.Cartesian.P = ParseF(PosP);
        p.Cartesian.R = ParseF(PosR);
        p.Joint.J1 = ParseF(PosJ1);
        p.Joint.J2 = ParseF(PosJ2);
        p.Joint.J3 = ParseF(PosJ3);
        p.Joint.J4 = ParseF(PosJ4);
        p.Joint.J5 = ParseF(PosJ5);
        p.Joint.J6 = ParseF(PosJ6);
        return PositionStore.ToWords(p);
    }

    private static float FloatToString(short[] words)
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

    private static short[] FloatToShorts(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new[]
        {
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2))
        };
    }

    private static float ParseF(string s) => float.TryParse(s, out float v) ? v : 0f;

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

/// <summary>系统变量读取结果。</summary>
public sealed class SysvarItem
{
    public string Name { get; }
    public string Type { get; }
    public string Value { get; }

    public SysvarItem(string name, string type, string value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}

/// <summary>系统变量类型（与客户端 VariableType 对齐）。</summary>
public enum SysvarType
{
    /// <summary>布尔（通过 INT 实现：word0 != 0 为真）。</summary>
    BOOL,
    /// <summary>32 位整数（2 字小端）。</summary>
    INT,
    /// <summary>单精度浮点（2 字）。</summary>
    REAL,
    /// <summary>字符串（40 字 NUL 结尾）。</summary>
    STRING,
    /// <summary>位置（50 字）。</summary>
    POS
}
