using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>高级页。</summary>
public partial class AdvancedView : UserControl
{
    public AdvancedView()
    {
        InitializeComponent();
    }

    public void SetViewModel(AdvancedViewModel vm) => DataContext = vm;
}
