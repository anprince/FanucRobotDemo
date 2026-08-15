using System.Windows;
using System.Windows.Threading;
using FanucRobotInterface.Server;
using FanucRobotInterface.Server.Simulation;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH;

/// <summary>
/// 主窗口。创建共享的模拟引擎与各页面 ViewModel。
/// </summary>
public partial class MainWindow : Window
{
    private readonly SimulatedController _controller = new();
    private readonly SnpxServer _server;

    public MainWindow()
    {
        InitializeComponent();

        // 初始化模拟引擎默认数据
        _controller.InitializeDefaults();

        // 服务器：传入 UI 线程的 SynchronizationContext，
        // 使 Clients 集合修改与日志事件都回到 UI 线程（避免 ObservableCollection 跨线程异常）。
        _server = new SnpxServer(_controller, System.Threading.SynchronizationContext.Current);

        // 各页面 ViewModel
        var dispatcher = Dispatcher.CurrentDispatcher;
        var connVm = new ConnectionViewModel(_server, dispatcher);
        var regVm = new RegisterSignalViewModel(_controller, dispatcher);
        var posVm = new PositionSysvarViewModel(_controller, dispatcher);
        var alarmVm = new AlarmTaskViewModel(_controller);
        var commentVm = new CommentViewModel(_controller, dispatcher);

        ConnectionPage.SetViewModel(connVm);
        RegisterSignalPage.SetViewModel(regVm);
        PositionSysvarPage.SetViewModel(posVm);
        AlarmTaskPage.SetViewModel(alarmVm);
        CommentPage.SetViewModel(commentVm);

        // 关闭时释放服务器
        Closed += (_, _) => _server.Dispose();
    }
}
