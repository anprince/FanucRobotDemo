using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>系统变量页。</summary>
public partial class SystemVarView : UserControl
{
    public SystemVarView()
    {
        InitializeComponent();
    }

    public void SetViewModel(SystemVarViewModel vm) => DataContext = vm;
}
