using System.Windows;
using System.Windows.Threading;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH;

/// <summary>
/// 主窗口。创建共享的 RobotClientService 与各页面 ViewModel，按 MVVM 模式装配。
/// </summary>
public partial class MainWindow : Window
{
    private readonly RobotClientService _service;

    public MainWindow()
    {
        InitializeComponent();

        // 共享客户端服务（持有 FanucRobotClient，管理连接/重连/日志）
        _service = new RobotClientService(Dispatcher.CurrentDispatcher);

        // 连接面板 + 日志绑定到服务
        DataContext = _service;

        // 各页面 ViewModel
        var connVm = new ConnectionViewModel(_service);
        var dioVm = new DigitalIOViewModel(_service);
        var regVm = new RegisterViewModel(_service);
        var posVm = new PositionViewModel(_service);
        var sysVm = new SystemVarViewModel(_service);
        var taskVm = new TaskAlarmViewModel(_service);
        var commentVm = new CommentViewModel(_service);
        var advVm = new AdvancedViewModel(_service);

        ConnectionBar.SetViewModel(connVm);
        DigitalIOPage.SetViewModel(dioVm);
        RegisterPage.SetViewModel(regVm);
        PositionPage.SetViewModel(posVm);
        SystemVarPage.SetViewModel(sysVm);
        TaskAlarmPage.SetViewModel(taskVm);
        CommentPage.SetViewModel(commentVm);
        AdvancedPage.SetViewModel(advVm);

        _service.Log("FANUC 机器人通信示例已启动");
        _service.Log($"库版本: 1.2.2 | 基于 SNPX 协议 | 目标框架: .NET 8.0");
        _service.Log("请在上方输入机器人 IP 和端口，点击 [连接] 开始通信");

        Closed += (_, _) => _service.Shutdown();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _service.ClearLog();
        _service.Log("日志已清除");
    }
}
