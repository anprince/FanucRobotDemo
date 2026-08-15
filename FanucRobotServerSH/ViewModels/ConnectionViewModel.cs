using System.Collections.ObjectModel;
using System.Windows.Threading;
using FanucRobotInterface.Server;

namespace FanucRobotServerSH.ViewModels;

/// <summary>
/// 连接监控页 ViewModel：监听控制、客户端列表、事件日志。
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase
{
    private readonly SnpxServer _server;
    private readonly Dispatcher _dispatcher;
    private string _portText = SnpxServer.DefaultPort.ToString();
    private string _listenStatus = "未监听";
    private bool _isListening;
    private bool _isStarting;
    private bool _isStopping;

    /// <summary>日志条目上限，防止内存膨胀。</summary>
    private const int MaxLogEntries = 500;

    public ConnectionViewModel(SnpxServer server, Dispatcher dispatcher)
    {
        _server = server;
        _dispatcher = dispatcher;
        StartCommand = new RelayCommandNoArg(Start, () => !_isListening && !_isStarting);
        StopCommand = new RelayCommandNoArg(Stop, () => _isListening && !_isStopping);
        ClearLogCommand = new RelayCommandNoArg(() => LogEntries.Clear());
        server.Log += OnLog;
        server.ClientChanged += OnClientChanged;
    }

    /// <summary>监听端口输入。</summary>
    public string PortText
    {
        get => _portText;
        set => SetProperty(ref _portText, value);
    }

    /// <summary>监听状态文本。</summary>
    public string ListenStatus
    {
        get => _listenStatus;
        set => SetProperty(ref _listenStatus, value);
    }

    /// <summary>是否正在监听。</summary>
    public bool IsListening
    {
        get => _isListening;
        set
        {
            if (SetProperty(ref _isListening, value))
            {
                CommandManagerInvalidate();
            }
        }
    }

    /// <summary>客户端列表。</summary>
    public ObservableCollection<ClientInfo> Clients => _server.Clients;

    /// <summary>日志。</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public RelayCommandNoArg StartCommand { get; }
    public RelayCommandNoArg StopCommand { get; }
    public RelayCommandNoArg ClearLogCommand { get; }

    private void Start()
    {
        if (_isStarting || _isListening)
        {
            return;
        }
        _isStarting = true;
        CommandManagerInvalidate();
        try
        {
            if (!int.TryParse(PortText, out int port) || port <= 0 || port > 65535)
            {
                AddLog("[Server] 端口无效，请输入 1-65535 的整数");
                return;
            }
            bool ok = _server.Start(port);
            if (ok)
            {
                IsListening = true;
                ListenStatus = $"监听中 (0.0.0.0:{port})";
            }
            else
            {
                ListenStatus = "启动失败";
            }
        }
        finally
        {
            _isStarting = false;
            CommandManagerInvalidate();
        }
    }

    private void Stop()
    {
        if (_isStopping || !_isListening)
        {
            return;
        }
        _isStopping = true;
        CommandManagerInvalidate();
        try
        {
            _server.Stop();
            IsListening = false;
            ListenStatus = "未监听";
        }
        finally
        {
            _isStopping = false;
            CommandManagerInvalidate();
        }
    }

    private void OnLog(string message) => _dispatcher.BeginInvoke(() => AddLog(message));

    private void AddLog(string message)
    {
        LogEntries.Add(new LogEntry { Message = message, Time = DateTime.Now });
        while (LogEntries.Count > MaxLogEntries)
        {
            LogEntries.RemoveAt(0);
        }
    }

    private void OnClientChanged() => _dispatcher.BeginInvoke(CommandManagerInvalidate);

    private static void CommandManagerInvalidate()
        => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
}

/// <summary>日志条目。</summary>
public sealed class LogEntry
{
    public string Message { get; set; } = "";
    public DateTime Time { get; set; }
    public string TimeText => Time.ToString("HH:mm:ss.fff");
    /// <summary>带时间戳的完整日志文本（UI 显示用）。</summary>
    public string DisplayText => $"[{TimeText}] {Message}";
}
