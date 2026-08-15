using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 高级页 ViewModel：ClearRAssignment / SetClientId / PR 批量读写 / PMC D 批量读写。
/// </summary>
public sealed class AdvancedViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _clientId = "1024";
    private string _prBatchStart = "1";
    private string _prBatchCount = "2";
    private string _prBatchResult = "-";
    private string _prJointBatchStart = "1";
    private string _prJointBatchCount = "2";
    private string _pmcDataBatchStart = "1";
    private string _pmcDataBatchCount = "3";
    private string _pmcDataBatchValues = "100,200,300";
    private string _pmcDataBatchResult = "-";

    public AdvancedViewModel(RobotClientService service)
    {
        _service = service;
        ClearRAssignmentCommand = new RelayCommandNoArg(ClearRAssignment);
        SetClientIdCommand = new RelayCommandNoArg(SetClientId);
        WritePrCartBatchCommand = new RelayCommandNoArg(WritePrCartBatch);
        ReadPrBatchCommand = new RelayCommandNoArg(ReadPrBatch);
        WritePrJointBatchCommand = new RelayCommandNoArg(WritePrJointBatch);
        ReadPmcDataBatchCommand = new RelayCommandNoArg(ReadPmcDataBatch);
        WritePmcDataBatchCommand = new RelayCommandNoArg(WritePmcDataBatch);
    }

    public string ClientId { get => _clientId; set => SetProperty(ref _clientId, value); }
    public string PrBatchStart { get => _prBatchStart; set => SetProperty(ref _prBatchStart, value); }
    public string PrBatchCount { get => _prBatchCount; set => SetProperty(ref _prBatchCount, value); }
    public string PrBatchResult { get => _prBatchResult; set => SetProperty(ref _prBatchResult, value); }
    public string PrJointBatchStart { get => _prJointBatchStart; set => SetProperty(ref _prJointBatchStart, value); }
    public string PrJointBatchCount { get => _prJointBatchCount; set => SetProperty(ref _prJointBatchCount, value); }
    public string PmcDataBatchStart { get => _pmcDataBatchStart; set => SetProperty(ref _pmcDataBatchStart, value); }
    public string PmcDataBatchCount { get => _pmcDataBatchCount; set => SetProperty(ref _pmcDataBatchCount, value); }
    public string PmcDataBatchValues { get => _pmcDataBatchValues; set => SetProperty(ref _pmcDataBatchValues, value); }
    public string PmcDataBatchResult { get => _pmcDataBatchResult; set => SetProperty(ref _pmcDataBatchResult, value); }

    public RelayCommandNoArg ClearRAssignmentCommand { get; }
    public RelayCommandNoArg SetClientIdCommand { get; }
    public RelayCommandNoArg WritePrCartBatchCommand { get; }
    public RelayCommandNoArg ReadPrBatchCommand { get; }
    public RelayCommandNoArg WritePrJointBatchCommand { get; }
    public RelayCommandNoArg ReadPmcDataBatchCommand { get; }
    public RelayCommandNoArg WritePmcDataBatchCommand { get; }

    private async void ClearRAssignment()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            bool ok = await _service.Robot.ClearRAssignmentAsync();
            _service.Log(ok ? "✅ 已清空 R 寄存器映射 (ClearRAssignment)" : "⚠️ ClearRAssignment 返回 false");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 清空 R 映射失败: {ex.Message}");
        }
    }

    private void SetClientId()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(ClientId, out var clientId)) return;
        try
        {
            _service.Robot.SetClientId(clientId);
            _service.Log($"✅ 客户端 ID 已设置为 {clientId}（下次连接生效）");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 设置 ClientId 失败: {ex.Message}");
        }
    }

    private async void WritePrCartBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PrBatchStart, out var start)) start = 1;
        if (!int.TryParse(PrBatchCount, out var count) || count < 1) count = 2;

        var cartesians = new List<CartesianPosition>();
        var configs = new List<PositionConfig>();
        for (int i = 0; i < count; i++)
        {
            cartesians.Add(new CartesianPosition { X = 100 + i * 100, Y = 200 + i * 10, Z = 300 + i * 10, W = 180, P = 0, R = 0 });
            configs.Add(new PositionConfig
            {
                NonFFlip = PositionConfig.FlipState.Flip,
                LeftRight = PositionConfig.HandConfig.Left,
                DownUp = PositionConfig.ArmConfig.Down,
                BackTurn = PositionConfig.TurnConfig.Turn
            });
        }
        try
        {
            await _service.Robot.PosReg.WriteCartesianBatchAsync(start, cartesians.ToArray(), configs.ToArray(), uf: 1, ut: 1);
            _service.Log($"✅ PR[{start}]~PR[{start + count - 1}] 笛卡尔坐标批量写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ PR 笛卡尔批量写入失败: {ex.Message}");
        }
    }

    private async void ReadPrBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PrBatchStart, out var start)) start = 1;
        if (!int.TryParse(PrBatchCount, out var count) || count < 1) count = 2;
        try
        {
            var positions = await _service.Robot.PosReg.ReadBatchAsync(start, count);
            var sb = new StringBuilder();
            for (int i = 0; i < positions.Length; i++)
            {
                sb.AppendLine($"PR[{start + i}]:");
                sb.AppendLine(FormatJoint("  关节", positions[i].Joint));
                sb.AppendLine(FormatCartesian("  笛卡尔", positions[i].Cartesian));
            }
            PrBatchResult = sb.ToString();
            _service.Log($"批量读取 PR[{start}]~PR[{start + count - 1}] 完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ PR 批量读取失败: {ex.Message}");
        }
    }

    private async void WritePrJointBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PrJointBatchStart, out var start)) start = 1;
        if (!int.TryParse(PrJointBatchCount, out var count) || count < 1) count = 2;

        var joints = new List<JointPosition>();
        for (int i = 0; i < count; i++)
        {
            joints.Add(new JointPosition { J1 = 10 * i, J2 = -30 + 5 * i, J3 = 60 - 5 * i, J4 = 0, J5 = 0, J6 = 0 });
        }
        try
        {
            await _service.Robot.PosReg.WriteJointBatchAsync(start, joints.ToArray(), uf: 1, ut: 1);
            _service.Log($"✅ PR[{start}]~PR[{start + count - 1}] 关节坐标批量写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ PR 关节批量写入失败: {ex.Message}");
        }
    }

    private async void ReadPmcDataBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PmcDataBatchStart, out var start)) start = 1;
        if (!int.TryParse(PmcDataBatchCount, out var count) || count < 1) count = 3;
        try
        {
            int[] vals = await _service.Robot.Pmc.ReadDatasAsync(start, count);
            PmcDataBatchResult = string.Join(", ", vals.Select((v, i) => $"D[{start + i}]={v}"));
            _service.Log($"批量读取 PMC D[{start}]~D[{start + count - 1}] 完成");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ PMC D 批量读取失败: {ex.Message}");
        }
    }

    private async void WritePmcDataBatch()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(PmcDataBatchStart, out var start)) start = 1;

        var values = new List<int>();
        foreach (var p in PmcDataBatchValues.Split(','))
        {
            if (int.TryParse(p.Trim(), out var v)) values.Add(v);
        }
        if (values.Count == 0) return;
        try
        {
            await _service.Robot.Pmc.WriteDatasAsync(start, values.ToArray());
            _service.Log($"✅ PMC D[{start}]~D[{start + values.Count - 1}] 批量写入成功");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ PMC D 批量写入失败: {ex.Message}");
        }
    }

    private static string FormatJoint(string label, JointPosition j)
        => $"  {label}: J1={j.J1,8:F2}  J2={j.J2,8:F2}  J3={j.J3,8:F2}  J4={j.J4,8:F2}  J5={j.J5,8:F2}  J6={j.J6,8:F2}";

    private static string FormatCartesian(string label, CartesianPosition c)
        => $"  {label}: X={c.X,8:F2}  Y={c.Y,8:F2}  Z={c.Z,8:F2}  W={c.W,8:F2}  P={c.P,8:F2}  R={c.R,8:F2}";
}
