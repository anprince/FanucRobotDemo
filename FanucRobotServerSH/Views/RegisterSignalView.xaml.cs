using System.Windows.Controls;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH.Views;

/// <summary>寄存器/信号编辑页。</summary>
public partial class RegisterSignalView : UserControl
{
    public RegisterSignalView()
    {
        InitializeComponent();
    }

    public void SetViewModel(RegisterSignalViewModel vm) => DataContext = vm;
}
