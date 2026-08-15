using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>位置页。</summary>
public partial class PositionView : UserControl
{
    public PositionView()
    {
        InitializeComponent();
    }

    public void SetViewModel(PositionViewModel vm) => DataContext = vm;
}
