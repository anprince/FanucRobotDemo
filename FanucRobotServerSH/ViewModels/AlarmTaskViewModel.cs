using System.Collections.ObjectModel;
using FanucRobotInterface.Common.Data;
using FanucRobotInterface.Server.Simulation;

namespace FanucRobotServerSH.ViewModels;

/// <summary>
/// 报警与任务页 ViewModel：配置报警列表与任务状态，供客户端读取测试。
/// </summary>
public sealed class AlarmTaskViewModel : ViewModelBase
{
    private readonly SimulatedController _controller;
    private string _newAlarmNumber = "1001";
    private string _newAlarmSeverity = "1";
    private string _newAlarmMessage = "Servo error";
    private string _newTaskProg = "MAIN";
    private string _newTaskLine = "1";
    private string _newTaskState = "0";
    private string _newTaskVar = "PRG[2]";

    public AlarmTaskViewModel(SimulatedController controller)
    {
        _controller = controller;
        Alarms = _controller.Alarms.Alarms;
        Tasks = _controller.Tasks.Tasks;
        AddAlarmCommand = new RelayCommandNoArg(AddAlarm);
        ClearAlarmsCommand = new RelayCommandNoArg(_controller.Alarms.Clear);
        RemoveAlarmCommand = new RelayCommand(RemoveAlarm);
        AddTaskCommand = new RelayCommandNoArg(AddTask);
        RemoveTaskCommand = new RelayCommand(RemoveTask);
    }

    public string NewAlarmNumber { get => _newAlarmNumber; set => SetProperty(ref _newAlarmNumber, value); }
    public string NewAlarmSeverity { get => _newAlarmSeverity; set => SetProperty(ref _newAlarmSeverity, value); }
    public string NewAlarmMessage { get => _newAlarmMessage; set => SetProperty(ref _newAlarmMessage, value); }
    public string NewTaskProg { get => _newTaskProg; set => SetProperty(ref _newTaskProg, value); }
    public string NewTaskLine { get => _newTaskLine; set => SetProperty(ref _newTaskLine, value); }
    public string NewTaskState { get => _newTaskState; set => SetProperty(ref _newTaskState, value); }
    public string NewTaskVar { get => _newTaskVar; set => SetProperty(ref _newTaskVar, value); }

    public ObservableCollection<AlarmItem> Alarms { get; }
    public ObservableCollection<TaskEntry> Tasks { get; }

    public RelayCommandNoArg AddAlarmCommand { get; }
    public RelayCommandNoArg ClearAlarmsCommand { get; }
    public RelayCommand RemoveAlarmCommand { get; }
    public RelayCommandNoArg AddTaskCommand { get; }
    public RelayCommand RemoveTaskCommand { get; }

    private void AddAlarm()
    {
        if (!int.TryParse(NewAlarmNumber, out int number))
        {
            number = 1000;
        }
        if (!int.TryParse(NewAlarmSeverity, out int severity))
        {
            severity = 1;
        }
        var now = DateTime.Now;
        _controller.Alarms.Add(new AlarmItem
        {
            AlarmNumber = (short)number,
            Severity = (short)severity,
            Year = (short)(now.Year - 2000),
            Month = (short)now.Month,
            Day = (short)now.Day,
            Hour = (short)now.Hour,
            Minute = (short)now.Minute,
            Second = (short)now.Second,
            AlarmMessage = NewAlarmMessage,
            CauseAlarmMessage = "",
            SeverityMessage = severity >= 2 ? "WARN" : "ERR"
        });
    }

    private void RemoveAlarm(object? param)
    {
        if (param is AlarmItem item)
        {
            _controller.Alarms.Remove(item);
        }
    }

    private void AddTask()
    {
        if (!int.TryParse(NewTaskLine, out int line))
        {
            line = 1;
        }
        if (!int.TryParse(NewTaskState, out int state))
        {
            state = 0;
        }
        string variable = string.IsNullOrWhiteSpace(NewTaskVar) ? $"PRG[{Tasks.Count + 1}]" : NewTaskVar.Trim();
        _controller.Tasks.Tasks.Add(new TaskEntry
        {
            Variable = variable,
            Task = new TaskInfo { ProgName = NewTaskProg, LineNumber = (short)line, State = (short)state }
        });
    }

    private void RemoveTask(object? param)
    {
        if (param is TaskEntry entry)
        {
            _controller.Tasks.Tasks.Remove(entry);
        }
    }
}
