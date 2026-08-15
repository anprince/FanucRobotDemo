using System.Windows.Controls;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH.Views;

/// <summary>连接监控页。</summary>
public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
    }

    /// <summary>设置数据上下文。</summary>
    public void SetViewModel(ConnectionViewModel vm) => DataContext = vm;
}
