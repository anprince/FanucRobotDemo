using System.Windows.Controls;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH.Views;

/// <summary>报警与任务页。</summary>
public partial class AlarmTaskView : UserControl
{
    public AlarmTaskView()
    {
        InitializeComponent();
    }

    public void SetViewModel(AlarmTaskViewModel vm) => DataContext = vm;
}
