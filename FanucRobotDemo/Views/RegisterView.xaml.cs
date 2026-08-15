using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>寄存器页。</summary>
public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    public void SetViewModel(RegisterViewModel vm) => DataContext = vm;
}
