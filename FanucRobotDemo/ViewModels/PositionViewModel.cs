using System.Text;
using FanucRobotInterface.Common.Data;
using FanucRobotInterface.Common.Extensions;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 位置页 ViewModel：世界/关节/UF 位置 + PR 读取与关节写入。
/// </summary>
public sealed class PositionViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _positionResult = "点击按钮读取机器人当前位置...";
    private string _ufNumber = "1";
    private string _prIndex = "1";
    private string _prGroup = "1";
    private string _prResult = "-";
    private string _prWriteIndex = "1";
    private string _j1 = "0";
    private string _j2 = "-90";
    private string _j3 = "0";
    private string _j4 = "0";
    private string _j5 = "0";
    private string _j6 = "0";
    private string _cartWriteIndex = "1";
    private string _cartUf = "1";
    private string _cartUt = "1";
    private string _x = "500";
    private string _y = "0";
    private string _z = "1000";
    private string _w = "180";
    private string _p = "0";
    private string _r = "0";

    public PositionViewModel(RobotClientService service)
    {
        _service = service;
        ReadWorldPosCommand = new RelayCommandNoArg(ReadWorldPos);
        ReadJointPosCommand = new RelayCommandNoArg(ReadJointPos);
        ReadUserPosCommand = new RelayCommandNoArg(ReadUserPos);
        ReadPrCommand = new RelayCommandNoArg(ReadPr);
        WritePrCommand = new RelayCommandNoArg(WritePr);
        WritePrCartesianCommand = new RelayCommandNoArg(WritePrCartesian);
    }

    public string PositionResult { get => _positionResult; set => SetProperty(ref _positionResult, value); }
    public string UfNumber { get => _ufNumber; set => SetProperty(ref _ufNumber, value); }
    public string PrIndex { get => _prIndex; set => SetProperty(ref _prIndex, value); }
    public string PrGroup { get => _prGroup; set => SetProperty(ref _prGroup, value); }
    public string PrResult { get => _prResult; set => SetProperty(ref _prResult, value); }
    public string PrWriteIndex { get => _prWriteIndex; set => SetProperty(ref _prWriteIndex, value); }
    public string J1 { get => _j1; set => SetProperty(ref _j1, value); }
    public string J2 { get => _j2; set => SetProperty(ref _j2, value); }
    public string J3 { get => _j3; set => SetProperty(ref _j3, value); }
    public string J4 { get => _j4; set => SetProperty(ref _j4, value); }
    public string J5 { get => _j5; set => SetProperty(ref _j5, value); }
    public string J6 { get => _j6; set => SetProperty(ref _j6, value); }
    public string CartWriteIndex { get => _cartWriteIndex; set => SetProperty(ref _cartWriteIndex, value); }
    public string CartUf { get => _cartUf; set => SetProperty(ref _cartUf, value); }
    public string CartUt { get => _cartUt; set => SetProperty(ref _cartUt, value); }
    public string X { get => _x; set => SetProperty(ref _x, value); }
    public string Y { get => _y; set => SetProperty(ref _y, value); }
    public string Z { get => _z; set => SetProperty(ref _z, value); }
    public string W { get => _w; set => SetProperty(ref _w, value); }
    public string P { get => _p; set => SetProperty(ref _p, value); }
    public string R { get => _r; set => SetProperty(ref _r, value); }

    public RelayCommandNoArg ReadWorldPosCommand { get; }
    public RelayCommandNoArg ReadJointPosCommand { get; }
    public RelayCommandNoArg ReadUserPosCommand { get; }
    public RelayCommandNoArg ReadPrCommand { get; }
    public RelayCommandNoArg WritePrCommand { get; }
    public RelayCommandNoArg WritePrCartesianCommand { get; }

    private async void ReadWorldPos()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var cart = await _service.Robot.Position.ReadWorldPositionAsync(group: 1);
            PositionResult = FormatCartesian("世界坐标 (Group 1)", cart);
            _service.Log($"读取世界坐标: X={cart.X:F1} Y={cart.Y:F1} Z={cart.Z:F1} W={cart.W:F1} P={cart.P:F1} R={cart.R:F1}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取世界坐标失败: {ex.Message}");
        }
    }

    private async void ReadJointPos()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var joint = await _service.Robot.Position.ReadJointPositionAsync(group: 1);
            PositionResult = FormatJoint("关节坐标 (Group 1)", joint);
            _service.Log($"读取关节坐标: J1={joint.J1:F1} J2={joint.J2:F1} J3={joint.J3:F1} J4={joint.J4:F1} J5={joint.J5:F1} J6={joint.J6:F1}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取关节坐标失败: {ex.Message}");
        }
    }

    private async void ReadUserPos()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!short.TryParse(UfNumber, out var uf)) uf = 1;
        try
        {
            var pos = await _service.Robot.Position.ReadUserPositionAsync(ufNumber: uf, group: 1);
            var sb = new StringBuilder();
            sb.AppendLine($"━━━ UF{uf} 位置 (Group 1) ━━━");
            sb.AppendLine(FormatJoint("关节坐标", pos.Joint));
            sb.AppendLine(FormatCartesian("笛卡尔坐标", pos.Cartesian));
            PositionResult = sb.ToString();
            _service.Log($"读取 UF{uf} 位置完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 UF 位置失败: {ex.Message}");
        }
    }

    private async void ReadPr()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(PrIndex, out var idx)) return;
        if (!int.TryParse(PrGroup, out var group)) group = 1;
        try
        {
            var pos = await _service.Robot.PosReg.ReadAsync(idx, group: group);
            var sb = new StringBuilder();
            sb.AppendLine($"━━━ PR[G{group}:{idx}] ━━━");
            if (pos.Joint.IsZero())
            {
                sb.AppendLine("⚠️ 关节坐标全零 — 可能该 PR 不存在或 Group 未配置");
            }
            sb.AppendLine(FormatJoint("关节坐标", pos.Joint));
            sb.AppendLine(FormatCartesian("笛卡尔坐标", pos.Cartesian));
            PrResult = sb.ToString();
            _service.Log($"读取 PR[G{group}:{idx}] 完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取 PR[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WritePr()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(PrWriteIndex, out var idx)) return;
        if (!float.TryParse(J1, out var j1)) return;
        if (!float.TryParse(J2, out var j2)) return;
        if (!float.TryParse(J3, out var j3)) return;
        if (!float.TryParse(J4, out var j4)) return;
        if (!float.TryParse(J5, out var j5)) return;
        if (!float.TryParse(J6, out var j6)) return;
        try
        {
            var joint = new JointPosition { J1 = j1, J2 = j2, J3 = j3, J4 = j4, J5 = j5, J6 = j6 };
            await _service.Robot.PosReg.WriteJointAsync(idx, joint, uf: 1, ut: 1);
            _service.Log($"✅ PR[{idx}] 关节坐标写入成功 (UF=1, UT=1)");
            _service.Log($"   J1={j1:F1} J2={j2:F1} J3={j3:F1} J4={j4:F1} J5={j5:F1} J6={j6:F1}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 PR[{idx}] 失败: {ex.Message}");
        }
    }

    private async void WritePrCartesian()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!TryParseIndex(CartWriteIndex, out var idx)) return;
        if (!float.TryParse(X, out var x)) return;
        if (!float.TryParse(Y, out var y)) return;
        if (!float.TryParse(Z, out var z)) return;
        if (!float.TryParse(W, out var w)) return;
        if (!float.TryParse(P, out var p)) return;
        if (!float.TryParse(R, out var r)) return;
        if (!short.TryParse(CartUf, out var uf)) uf = 1;
        if (!short.TryParse(CartUt, out var ut)) ut = 1;
        try
        {
            var cart = new CartesianPosition { X = x, Y = y, Z = z, W = w, P = p, R = r };
            var config = new PositionConfig
            {
                NonFFlip = PositionConfig.FlipState.NonFlip,
                LeftRight = PositionConfig.HandConfig.Left,
                DownUp = PositionConfig.ArmConfig.Down,
                BackTurn = PositionConfig.TurnConfig.Back
            };
            await _service.Robot.PosReg.WriteCartesianAsync(idx, cart, config, uf: uf, ut: ut);
            _service.Log($"✅ PR[{idx}] 世界坐标写入成功 (UF={uf}, UT={ut})");
            _service.Log($"   X={x:F2} Y={y:F2} Z={z:F2} W={w:F2} P={p:F2} R={r:F2}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入 PR[{idx}] 世界坐标失败: {ex.Message}");
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

    private static string FormatJoint(string label, JointPosition j)
        => $"  {label}: J1={j.J1,8:F2}  J2={j.J2,8:F2}  J3={j.J3,8:F2}  J4={j.J4,8:F2}  J5={j.J5,8:F2}  J6={j.J6,8:F2}";

    private static string FormatCartesian(string label, CartesianPosition c)
        => $"  {label}: X={c.X,8:F2}  Y={c.Y,8:F2}  Z={c.Z,8:F2}  W={c.W,8:F2}  P={c.P,8:F2}  R={c.R,8:F2}";
}
