using System.Windows.Controls;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH.Views;

/// <summary>位置/系统变量页。</summary>
public partial class PositionSysvarView : UserControl
{
    public PositionSysvarView()
    {
        InitializeComponent();
    }

    public void SetViewModel(PositionSysvarViewModel vm) => DataContext = vm;
}
