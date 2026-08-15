using System.Collections.ObjectModel;
using System.Windows.Threading;
using FanucRobotInterface;
using FanucRobotInterface.Common.Configuration;
using FanucRobotInterface.Common.Exceptions;

namespace FanucRobotDemoSH.ViewModels;

/// <summary>
/// 共享客户端服务：持有唯一的 FanucRobotClient 实例，统一管理
/// 连接 / 断开 / 自动重连 / 日志 / 状态通知。所有页面 ViewModel 通过此服务访问客户端。
/// </summary>
public sealed class RobotClientService : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private FanucRobotClient? _robot;

    // 自动重连
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private bool _manualDisconnect;

    private string _ip = "127.0.0.1";
    private string _port = "60008";
    private string _statusText = "未连接";
    private string _statusBackground = "#E0E0E0";
    private bool _isConnected;
    private bool _isReconnecting;
    private bool _isConnectEnabled = true;

    public RobotClientService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        ConnectCommand = new RelayCommandNoArg(Connect);
        DisconnectCommand = new RelayCommandNoArg(Disconnect);
    }

    /// <summary>当前客户端实例（未连接时为 null）。</summary>
    public FanucRobotClient? Robot => _robot;

    /// <summary>日志集合（MainWindow 日志区绑定）。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    public string Ip { get => _ip; set => SetProperty(ref _ip, value); }
    public string Port { get => _port; set => SetProperty(ref _port, value); }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string StatusBackground { get => _statusBackground; private set => SetProperty(ref _statusBackground, value); }

    public bool IsConnected { get => _isConnected; private set => SetProperty(ref _isConnected, value); }
    public bool IsReconnecting { get => _isReconnecting; private set => SetProperty(ref _isReconnecting, value); }
    public bool IsConnectEnabled { get => _isConnectEnabled; private set => SetProperty(ref _isConnectEnabled, value); }
    public bool IsDisconnectEnabled => IsConnected || IsReconnecting;

    public RelayCommandNoArg ConnectCommand { get; }
    public RelayCommandNoArg DisconnectCommand { get; }

    // ---- 连接管理 ----

    private async void Connect()
    {
        try
        {
            var config = BuildConfig(out var ip, out var port);
            _manualDisconnect = false;
            Log($"正在连接 {ip}:{port} ...");

            var robot = new FanucRobotClient(config);
            bool connected = await robot.ConnectAsync();

            if (connected)
            {
                AttachRobot(robot);
                Log($"✅ 成功连接到机器人 {ip}:{port}");
            }
            else
            {
                robot.Dispose();
                UpdateStatus(ConnState.Failed);
                Log($"❌ 连接失败: 返回 false");
            }
        }
        catch (RobotException ex)
        {
            CleanupRobot();
            UpdateStatus(ConnState.Failed);
            Log($"❌ 连接错误 [{ex.ErrorCode}]: {ex.Message}");
        }
        catch (Exception ex)
        {
            CleanupRobot();
            UpdateStatus(ConnState.Failed);
            Log($"❌ 未知错误: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        try
        {
            _manualDisconnect = true;
            StopReconnect();
            if (_robot != null)
            {
                _robot.ConnectionLost -= OnConnectionLost;
                _robot.Disconnect();
                _robot.Dispose();
            }
            _robot = null;
            UpdateStatus(ConnState.Disconnected);
            Log("已断开连接");
        }
        catch (Exception ex)
        {
            Log($"断开连接时出错: {ex.Message}");
        }
    }

    /// <summary>断线检测（后台线程触发）→ 自动重连。</summary>
    private void OnConnectionLost()
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_robot != null)
            {
                _robot.ConnectionLost -= OnConnectionLost;
                _robot.Dispose();
                _robot = null;
            }
            if (_manualDisconnect)
            {
                UpdateStatus(ConnState.Disconnected);
                return;
            }
            Log("⚠️ 连接已断开，开始自动重连...");
            StartReconnect();
        });
    }

    private void AttachRobot(FanucRobotClient robot)
    {
        _robot = robot;
        _robot.ConnectionLost += OnConnectionLost;
        UpdateStatus(ConnState.Connected);
        Log($"[DIAG] StringEncoding = {_robot.StringEncoding.EncodingName} (CodePage={_robot.StringEncoding.CodePage})");
    }

    private void CleanupRobot()
    {
        _robot?.Dispose();
        _robot = null;
    }

    private void StartReconnect()
    {
        StopReconnect();
        var cts = new CancellationTokenSource();
        _reconnectCts = cts;
        _manualDisconnect = false;
        UpdateStatus(ConnState.Reconnecting);

        var config = BuildConfig(out var ip, out var port);

        const int initialIntervalSec = 3;
        const int maxIntervalSec = 300;
        int intervalSec = initialIntervalSec;

        _reconnectTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bool needBackoff = false;
                try
                {
                    var robot = new FanucRobotClient(config);
                    bool ok = await robot.ConnectAsync();
                    if (ok && !cts.IsCancellationRequested)
                    {
                        _ = _dispatcher.BeginInvoke(() =>
                        {
                            if (cts.IsCancellationRequested)
                            {
                                robot.Dispose();
                                return;
                            }
                            AttachRobot(robot);
                            Log($"✅ 自动重连成功: {ip}:{port}");
                            StopReconnect();
                        });
                        return;
                    }
                    robot.Dispose();
                    needBackoff = true;
                }
                catch (RobotException ex)
                {
                    Log($"⏳ 重连失败 [{ex.ErrorCode}]: {ex.Message}，{intervalSec} 秒后重试");
                    needBackoff = true;
                }
                catch (Exception ex)
                {
                    Log($"⏳ 重连失败: {ex.Message}，{intervalSec} 秒后重试");
                    needBackoff = true;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSec), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (needBackoff)
                {
                    intervalSec = Math.Min(intervalSec * 2, maxIntervalSec);
                }
            }
        });
    }

    private void StopReconnect()
    {
        var cts = _reconnectCts;
        _reconnectCts = null;
        cts?.Cancel();
        try
        {
            _reconnectTask?.Wait(TimeSpan.FromMilliseconds(200));
        }
        catch
        {
            // 忽略
        }
        _reconnectTask = null;
    }

    private RobotConnectionConfig BuildConfig(out string ip, out int port)
    {
        ip = Ip.Trim();
        if (!int.TryParse(Port.Trim(), out port))
        {
            port = 60008;
        }
        return new RobotConnectionConfig
        {
            IpAddress = ip,
            Port = port,
            ConnectionTimeoutMs = 10000,
            ReadWriteTimeoutMs = 10000
        };
    }

    private void UpdateStatus(ConnState state)
    {
        (string text, string bgHex, bool reconnecting) = state switch
        {
            ConnState.Connected => ("已连接", "#E8F5E9", false),
            ConnState.Reconnecting => ("重连中...", "#FFF3E0", true),
            ConnState.Failed => ("连接失败", "#FFEBEE", false),
            _ => ("未连接", "#E0E0E0", false),
        };
        StatusText = text;
        StatusBackground = bgHex;
        IsReconnecting = reconnecting;
        IsConnected = state == ConnState.Connected;
        IsConnectEnabled = !reconnecting;
        OnPropertyChanged(nameof(IsDisconnectEnabled));
    }

    /// <summary>关闭时清理。</summary>
    public void Shutdown()
    {
        try
        {
            StopReconnect();
            _robot?.Disconnect();
            _robot?.Dispose();
        }
        catch
        {
            // 忽略清理错误
        }
    }

    // ---- 日志 ----

    /// <summary>写入日志（线程安全）。</summary>
    public void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var entry = $"[{time}] {message}";
        _dispatcher.BeginInvoke(() =>
        {
            Logs.Add(entry);
            if (Logs.Count > 2000)
            {
                // 防止日志无限增长
                for (int i = 0; i < 500; i++)
                {
                    Logs.RemoveAt(0);
                }
            }
        });
    }

    /// <summary>清除日志。</summary>
    public void ClearLog()
    {
        _dispatcher.BeginInvoke(() => Logs.Clear());
    }

    /// <summary>确保已连接，未连接则记录提示。返回是否已连接。</summary>
    public bool EnsureConnected()
    {
        if (_robot == null || !_robot.IsConnected)
        {
            Log("⚠️ 未连接到机器人，请先点击 [连接]");
            return false;
        }
        return true;
    }
}

/// <summary>连接状态标志位。</summary>
public enum ConnState
{
    Disconnected,
    Connected,
    Reconnecting,
    Failed,
}
