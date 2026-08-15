using FanucRobotInterface.Common.Data;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 连接/快捷操作页 ViewModel：包装 RobotClientService 的连接命令 + 快捷读取操作。
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    public ConnectionViewModel(RobotClientService service)
    {
        _service = service;
        QuickReadR1Command = new RelayCommandNoArg(QuickReadR1);
        QuickReadJointCommand = new RelayCommandNoArg(QuickReadJoint);
        QuickReadDiCommand = new RelayCommandNoArg(QuickReadDi);
        QuickReadTaskCommand = new RelayCommandNoArg(QuickReadTask);
        QuickReadAlarmCommand = new RelayCommandNoArg(QuickReadAlarm);
    }

    /// <summary>连接/断开命令直接转发到共享服务。</summary>
    public RelayCommandNoArg ConnectCommand => _service.ConnectCommand;
    public RelayCommandNoArg DisconnectCommand => _service.DisconnectCommand;

    public RelayCommandNoArg QuickReadR1Command { get; }
    public RelayCommandNoArg QuickReadJointCommand { get; }
    public RelayCommandNoArg QuickReadDiCommand { get; }
    public RelayCommandNoArg QuickReadTaskCommand { get; }
    public RelayCommandNoArg QuickReadAlarmCommand { get; }

    private async void QuickReadR1()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            float val = await _service.Robot.NumReg.ReadAsync(1);
            _service.Log($"📖 R[1] = {val:F3}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ {ex.Message}");
        }
    }

    private async void QuickReadJoint()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var joint = await _service.Robot.Position.ReadJointPositionAsync(group: 1);
            _service.Log($"📖 当前关节: J1={joint.J1:F1} J2={joint.J2:F1} J3={joint.J3:F1} J4={joint.J4:F1} J5={joint.J5:F1} J6={joint.J6:F1}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ {ex.Message}");
        }
    }

    private async void QuickReadDi()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            bool[] vals = await _service.Robot.DI.ReadAsync(1, 8);
            _service.Log($"📖 DI[1-8]: [{string.Join(", ", vals.Select(v => v ? "1" : "0"))}]");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ {ex.Message}");
        }
    }

    private async void QuickReadTask()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var task = await _service.Robot.Task.ReadAsync(index: 1);
            _service.Log($"📖 任务1: {task.ProgName} 行{task.LineNumber} [{task.StateText}]");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ {ex.Message}");
        }
    }

    private async void QuickReadAlarm()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            var alarms = await _service.Robot.Alarm.ReadAsync(count: 5, type: AlarmType.Current, mode: AlarmMessageMode.Short);
            _service.Log($"📖 当前报警: {alarms.Length} 条");
            foreach (var a in alarms)
            {
                _service.Log($"  #{a.AlarmNumber}: {a.AlarmMessage}");
            }
        }
        catch (Exception ex)
        {
            _service.Log($"❌ {ex.Message}");
        }
    }
}
