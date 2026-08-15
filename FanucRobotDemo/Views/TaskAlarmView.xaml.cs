using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>任务与报警页。</summary>
public partial class TaskAlarmView : UserControl
{
    public TaskAlarmView()
    {
        InitializeComponent();
    }

    public void SetViewModel(TaskAlarmViewModel vm) => DataContext = vm;
}
