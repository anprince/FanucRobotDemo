using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>快捷操作栏。</summary>
public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
    }

    public void SetViewModel(ConnectionViewModel vm) => DataContext = vm;
}
