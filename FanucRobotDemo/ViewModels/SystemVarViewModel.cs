using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 系统变量页 ViewModel：INT/REAL/BOOL/STRING/POS 读取 + 写入 + 批量变量组。
/// </summary>
public sealed class SystemVarViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _varName = "$SYSNAME";
    private int _varTypeIndex = 3;
    private string _varResult = "-";
    private string _writeName = "$MOR_GRP[1].$ANGTOL[1]";
    private string _writeValue = "5";
    private string _varGroupResult = "-";

    public SystemVarViewModel(RobotClientService service)
    {
        _service = service;
        ReadCommand = new RelayCommandNoArg(Read);
        WriteCommand = new RelayCommandNoArg(Write);
        ReadGroupCommand = new RelayCommandNoArg(ReadGroup);
        PresetCommand = new RelayCommand(Preset);
    }

    public string VarName { get => _varName; set => SetProperty(ref _varName, value); }
    public int VarTypeIndex { get => _varTypeIndex; set => SetProperty(ref _varTypeIndex, value); }
    public string VarResult { get => _varResult; set => SetProperty(ref _varResult, value); }
    public string WriteName { get => _writeName; set => SetProperty(ref _writeName, value); }
    public string WriteValue { get => _writeValue; set => SetProperty(ref _writeValue, value); }
    public string VarGroupResult { get => _varGroupResult; set => SetProperty(ref _varGroupResult, value); }

    public RelayCommandNoArg ReadCommand { get; }
    public RelayCommandNoArg WriteCommand { get; }
    public RelayCommandNoArg ReadGroupCommand { get; }
    public RelayCommand PresetCommand { get; }

    private async void Read()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        var varName = VarName.Trim();
        if (string.IsNullOrEmpty(varName)) return;
        try
        {
            string result;
            switch (VarTypeIndex)
            {
                case 0:
                    result = $"{varName} = {await _service.Robot.SystemVariables.ReadIntAsync(varName)} (INT)";
                    break;
                case 1:
                    result = $"{varName} = {await _service.Robot.SystemVariables.ReadFloatAsync(varName):F4} (REAL)";
                    break;
                case 2:
                    result = $"{varName} = {(await _service.Robot.SystemVariables.ReadBoolAsync(varName) ? "ON" : "OFF")} (BOOL)";
                    break;
                case 3:
                    result = $"{varName} = \"{await _service.Robot.SystemVariables.ReadStringAsync(varName)}\" (STRING)";
                    break;
                case 4:
                {
                    var pos = await _service.Robot.SystemVariables.ReadPositionAsync(varName);
                    result = $"{varName} (POS) →\n{FormatJoint(pos.Joint)}\n{FormatCartesian(pos.Cartesian)}";
                    break;
                }
                default:
                    result = "未知类型";
                    break;
            }
            VarResult = result;
            _service.Log($"读取系统变量: {varName}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取系统变量失败: {ex.Message}");
            VarResult = $"错误: {ex.Message}";
        }
    }

    private async void Write()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        var varName = WriteName.Trim();
        if (string.IsNullOrEmpty(varName)) return;
        if (!int.TryParse(WriteValue, out var value)) return;
        try
        {
            await _service.Robot.SystemVariables.WriteIntAsync(varName, value);
            _service.Log($"✅ 系统变量 {varName} = {value} 写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 写入系统变量失败: {ex.Message}");
        }
    }

    private async void ReadGroup()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var group = _service.Robot.SystemVariables.CreateVariableGroup(new List<(string, VariableType)>
            {
                ("$MOR_GRP[1].$ANGTOL[1]", VariableType.INT),
                ("$SCRN_OP[1].$MAX_RPM",   VariableType.REAL),
                ("$SYSNAME",                VariableType.STRING)
            });
            object[] batch = await group.ReadAsync();

            var sb = new StringBuilder();
            sb.AppendLine("━━━ 批量变量组读取结果 ━━━");
            sb.AppendLine($"$MOR_GRP[1].$ANGTOL[1] = {batch[0]} (INT)");
            sb.AppendLine($"$SCRN_OP[1].$MAX_RPM   = {batch[1]} (REAL)");
            sb.AppendLine($"$SYSNAME                = \"{batch[2]}\" (STRING)");
            sb.AppendLine("(以上仅 1 次 SNPX READ 交互)");
            VarGroupResult = sb.ToString();
            _service.Log("批量变量组读取完成 (1次交互)");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 批量变量组失败: {ex.Message}");
        }
    }

    private void Preset(object? parameter)
    {
        if (parameter is not string varName)
        {
            return;
        }
        VarName = varName;
        VarTypeIndex = varName switch
        {
            "$SYSNAME" => 3,
            "$SS_ENB" => 2,
            _ when varName.StartsWith("$MNUFRAME") => 4,
            _ when varName.Contains("ANGTOL") => 0,
            _ when varName.Contains("MAX_RPM") => 1,
            _ => 3
        };
        Read();
    }

    private static string FormatJoint(JointPosition j)
        => $"关节: J1={j.J1,8:F2}  J2={j.J2,8:F2}  J3={j.J3,8:F2}  J4={j.J4,8:F2}  J5={j.J5,8:F2}  J6={j.J6,8:F2}";

    private static string FormatCartesian(CartesianPosition c)
        => $"笛卡尔: X={c.X,8:F2}  Y={c.Y,8:F2}  Z={c.Z,8:F2}  W={c.W,8:F2}  P={c.P,8:F2}  R={c.R,8:F2}";
}
