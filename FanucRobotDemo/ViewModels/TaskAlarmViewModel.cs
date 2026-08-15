using System.Text;
using FanucRobotInterface.Common.Data;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 任务与报警页 ViewModel：任务状态读取 + 报警读取/清除。
/// </summary>
public sealed class TaskAlarmViewModel : ViewModelBase
{
    private readonly RobotClientService _service;

    private string _taskIndex = "1";
    private string _taskResult = "点击读取任务状态...";
    private string _alarmCount = "10";
    private int _alarmTypeIndex;
    private string _alarmResult = "点击读取报警信息...";

    public TaskAlarmViewModel(RobotClientService service)
    {
        _service = service;
        ReadTaskCommand = new RelayCommandNoArg(ReadTask);
        ReadAlarmCommand = new RelayCommandNoArg(ReadAlarm);
        ClearAlarmCommand = new RelayCommandNoArg(ClearAlarm);
    }

    public string TaskIndex { get => _taskIndex; set => SetProperty(ref _taskIndex, value); }
    public string TaskResult { get => _taskResult; set => SetProperty(ref _taskResult, value); }
    public string AlarmCount { get => _alarmCount; set => SetProperty(ref _alarmCount, value); }
    public int AlarmTypeIndex { get => _alarmTypeIndex; set => SetProperty(ref _alarmTypeIndex, value); }
    public string AlarmResult { get => _alarmResult; set => SetProperty(ref _alarmResult, value); }

    public RelayCommandNoArg ReadTaskCommand { get; }
    public RelayCommandNoArg ReadAlarmCommand { get; }
    public RelayCommandNoArg ClearAlarmCommand { get; }

    private async void ReadTask()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(TaskIndex, out var idx)) idx = 1;
        try
        {
            var task = await _service.Robot.Task.ReadAsync(index: idx);
            var sb = new StringBuilder();
            sb.AppendLine($"━━━ 任务 {idx} 状态 ━━━");
            sb.AppendLine($"程序名:   {task.ProgName}");
            sb.AppendLine($"行号:     {task.LineNumber}");
            sb.AppendLine($"状态码:   {task.State} ({task.StateText})");
            sb.AppendLine($"父程序:   {task.ParentProgName}");
            TaskResult = sb.ToString();
            _service.Log($"读取任务 {idx}: {task.ProgName} - {task.StateText}");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取任务失败: {ex.Message}");
        }
    }

    private async void ReadAlarm()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        if (!int.TryParse(AlarmCount, out var count)) count = 10;
        try
        {
            AlarmType alarmType;
            AlarmMessageMode alarmMode;
            switch (AlarmTypeIndex)
            {
                case 0: alarmType = AlarmType.List; alarmMode = AlarmMessageMode.Full; break;
                case 1: alarmType = AlarmType.Current; alarmMode = AlarmMessageMode.Short; break;
                case 2: alarmType = AlarmType.Current; alarmMode = AlarmMessageMode.Full; break;
                default: alarmType = AlarmType.List; alarmMode = AlarmMessageMode.Full; break;
            }

            AlarmItem[] alarms = await _service.Robot.Alarm.ReadAsync(count: count, type: alarmType, mode: alarmMode);
            if (alarms.Length == 0)
            {
                AlarmResult = "✅ 无报警";
                _service.Log("读取报警: 无报警记录");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"━━━ 报警列表 (共 {alarms.Length} 条) ━━━");
            foreach (var alarm in alarms)
            {
                sb.AppendLine($"┌ 编号:{alarm.AlarmNumber}  ID:{alarm.AlarmId}  严重级别:{alarm.Severity}");
                sb.AppendLine($"├ 消息: {alarm.AlarmMessage}");
                if (!string.IsNullOrEmpty(alarm.CauseAlarmMessage))
                    sb.AppendLine($"├ 原因: {alarm.CauseAlarmMessage}");
                if (!string.IsNullOrEmpty(alarm.SeverityMessage))
                    sb.AppendLine($"├ 级别: {alarm.SeverityMessage}");
                sb.AppendLine(alarm.Timestamp.HasValue
                    ? $"└ 时间: {alarm.Timestamp:yyyy-MM-dd HH:mm:ss}"
                    : "└ 时间: -");
            }
            AlarmResult = sb.ToString();
            _service.Log($"读取报警: {alarms.Length} 条");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 读取报警失败: {ex.Message}");
        }
    }

    private async void ClearAlarm()
    {
        if (!_service.EnsureConnected() || _service.Robot == null) return;
        try
        {
            await _service.Robot.ClearAlarmAsync(type: 0);
            _service.Log("✅ 当前报警已清除");
        }
        catch (Exception ex)
        {
            _service.Log($"❌ 清除报警失败: {ex.Message}");
        }
    }
}
