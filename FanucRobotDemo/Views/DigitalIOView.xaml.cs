using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>数字 I/O 页。</summary>
public partial class DigitalIOView : UserControl
{
    public DigitalIOView()
    {
        InitializeComponent();
    }

    public void SetViewModel(DigitalIOViewModel vm) => DataContext = vm;
}
