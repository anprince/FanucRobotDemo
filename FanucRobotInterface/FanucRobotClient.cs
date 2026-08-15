using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common;
using FanucRobotInterface.Common.Configuration;
using FanucRobotInterface.Common.Data;
using FanucRobotInterface.Common.Signals;

namespace FanucRobotInterface;

/// <summary>
/// FANUC 机器人客户端（SNPX 协议）。
/// </summary>
public class FanucRobotClient : IFanucRobotClient
{
    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly RobotConnectionConfig _config;

    private readonly PositionDataManager _position;
    private readonly SystemVariablesManager _systemVariables;
    private readonly NumRegManager _numReg;
    private readonly PosRegManager _posReg;
    private readonly StrRegManager _strReg;
    private readonly FlagManager _flag;
    private readonly TaskManager _task;
    private readonly AlarmManager _alarm;
    private readonly CommentManager _comment;

    private readonly DigitalSignal _di, _do, _ri, _ro, _ui, _uo, _si, _so;
    private readonly DigitalSignal _wi, _wo, _wsi, _wso;
    private readonly GroupSignal _gi, _go;
    private readonly AnalogSignal _ai, _ao;
    private readonly PmcSignal _pmc;

/// <summary>初始化实例。</summary>
    public FanucRobotClient()
        : this(RobotConnectionConfig.Default)
    {
    }

/// <summary>初始化实例。</summary>
    public FanucRobotClient(RobotConnectionConfig config)
    {
        _config = config ?? RobotConnectionConfig.Default;
        _client = new SnpxProtocol();
        _allocator = _client.RegisterMap;

        _position = new PositionDataManager(_client, _allocator);
        _systemVariables = new SystemVariablesManager(_client, _allocator);
        _numReg = new NumRegManager(_client, _allocator);
        _posReg = new PosRegManager(_client, _allocator);
        _strReg = new StrRegManager(_client, _allocator);
        _flag = new FlagManager(_client, _allocator);
        _task = new TaskManager(_client, _allocator);
        _alarm = new AlarmManager(_client, _allocator);
        _comment = new CommentManager(_client, _allocator);

        _di = new DigitalSignal(_client, SignalCategory.DI);
        _do = new DigitalSignal(_client, SignalCategory.DO);
        _ri = new DigitalSignal(_client, SignalCategory.RI);
        _ro = new DigitalSignal(_client, SignalCategory.RO);
        _ui = new DigitalSignal(_client, SignalCategory.UI);
        _uo = new DigitalSignal(_client, SignalCategory.UO);
        _si = new DigitalSignal(_client, SignalCategory.SI);
        _so = new DigitalSignal(_client, SignalCategory.SO);
        _wi = new DigitalSignal(_client, SignalCategory.WI);
        _wo = new DigitalSignal(_client, SignalCategory.WO);
        _wsi = new DigitalSignal(_client, SignalCategory.WSI);
        _wso = new DigitalSignal(_client, SignalCategory.WSO);
        _gi = new GroupSignal(_client, SignalCategory.GI);
        _go = new GroupSignal(_client, SignalCategory.GO);
        _ai = new AnalogSignal(_client, SignalCategory.AI);
        _ao = new AnalogSignal(_client, SignalCategory.AO);
        _pmc = new PmcSignal(_client);

        // 转发断线检测事件
        _client.ConnectionLost += OnConnectionLost;
    }

    /// <summary>连接被对端关闭/网络异常导致断开时触发（后台线程触发，订阅者需 marshal 回 UI 线程）。</summary>
    public event Action ConnectionLost;

    private void OnConnectionLost() => ConnectionLost?.Invoke();

    // ---- 管理器属性 ----

    /// <summary>位置数据管理器（关节/笛卡尔坐标，PR 读写）。</summary>
    public PositionDataManager Position => _position;
    /// <summary>系统变量管理器（$VAR 读写，支持整数/浮点/字符串/位置类型）。</summary>
    public SystemVariablesManager SystemVariables => _systemVariables;
    /// <summary>数值寄存器管理器（R[i] 读写）。</summary>
    public NumRegManager NumReg => _numReg;
    /// <summary>位置寄存器管理器（PR[i] 读写，支持关节/笛卡尔坐标及批量操作）。</summary>
    public PosRegManager PosReg => _posReg;
    /// <summary>字符串寄存器管理器（SR[i] 读写）。</summary>
    public StrRegManager StrReg => _strReg;
    /// <summary>标志寄存器管理器（F[i] 读写）。</summary>
    public FlagManager Flag => _flag;
    /// <summary>任务状态管理器（PRG[i] 任务监控）。</summary>
    public TaskManager Task => _task;
    /// <summary>报警管理器（ALM[i] 报警历史/当前报警读取）。</summary>
    public AlarmManager Alarm => _alarm;
    /// <summary>注释管理器（COMMENT[i] 读取）。</summary>
    public CommentManager Comment => _comment;

    // ---- 信号属性 ----

    /// <summary>数字输入信号 DI[1]~DI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal DI => _di;
    /// <summary>数字输出信号 DO[1]~DO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal DO => _do;
    /// <summary>机器人输入信号 RI[1]~RI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal RI => _ri;
    /// <summary>机器人输出信号 RO[1]~RO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal RO => _ro;
    /// <summary>用户输入信号 UI[1]~UI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal UI => _ui;
    /// <summary>用户输出信号 UO[1]~UO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal UO => _uo;
    /// <summary>操作员输入信号 SI[1]~SI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal SI => _si;
    /// <summary>操作员输出信号 SO[1]~SO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal SO => _so;
    /// <summary>焊接输入信号 WI[1]~WI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal WI => _wi;
    /// <summary>焊接输出信号 WO[1]~WO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal WO => _wo;
    /// <summary>焊接状态输入信号 WSI[1]~WSI[n]。true=ON，false=OFF。</summary>
    public DigitalSignal WSI => _wsi;
    /// <summary>焊接状态输出信号 WSO[1]~WSO[n]。true=ON，false=OFF。</summary>
    public DigitalSignal WSO => _wso;
    /// <summary>组输入信号 GI[1]~GI[n]（16 位无符号，0~65535）。</summary>
    public GroupSignal GI => _gi;
    /// <summary>组输出信号 GO[1]~GO[n]（16 位无符号，0~65535）。</summary>
    public GroupSignal GO => _go;
    /// <summary>模拟输入信号 AI[1]~AI[n]（32 位有符号整数）。</summary>
    public AnalogSignal AI => _ai;
    /// <summary>模拟输出信号 AO[1]~AO[n]（32 位有符号整数）。</summary>
    public AnalogSignal AO => _ao;
    /// <summary>PMC 信号管理器（R 区/K 区/D 区及 PMCR2 参数）。</summary>
    public PmcSignal Pmc => _pmc;

    // ---- 连接 ----

    /// <summary>是否已连接到机器人控制器。</summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// 字符串编码（用于注释/字符串寄存器/系统变量字符串的字节解码）。
    /// 默认 GBK(936)（中文 FANUC 控制器常用）；其它控制器可重新赋值。
    /// </summary>
    public System.Text.Encoding StringEncoding
    {
        get => _client.StringEncoding;
        set => _client.StringEncoding = value;
    }

    /// <summary>连接到机器人控制器（同步）。</summary>
    /// <param name="ipAddress">控制器 IP 地址。</param>
    /// <param name="port">端口号，默认 60008。</param>
    /// <returns>连接成功返回 true，否则返回 false。</returns>
    public bool Connect(string ipAddress, int port = 60008)
    {
        return _client.Connect(ipAddress, port, _config.ConnectionTimeoutMs);
    }

    /// <summary>使用配置中的地址和端口连接到机器人控制器（同步）。</summary>
    /// <returns>连接成功返回 true，否则返回 false。</returns>
    public bool Connect()
    {
        return Connect(_config.IpAddress, _config.Port);
    }

    /// <summary>异步连接到机器人控制器。</summary>
    /// <param name="ipAddress">控制器 IP 地址。</param>
    /// <param name="port">端口号，默认 60008。</param>
    /// <returns>连接成功返回 true，否则返回 false。</returns>
    public Task<bool> ConnectAsync(string ipAddress, int port = 60008)
    {
        return _client.ConnectAsync(ipAddress, port, _config.ConnectionTimeoutMs);
    }

    /// <summary>使用配置中的地址和端口异步连接到机器人控制器。</summary>
    /// <returns>连接成功返回 true，否则返回 false。</returns>
    public Task<bool> ConnectAsync()
    {
        return ConnectAsync(_config.IpAddress, _config.Port);
    }

    /// <summary>断开与机器人控制器的连接。</summary>
    public void Disconnect()
    {
        _client.Disconnect();
    }

    // ---- 其他 ----

    /// <summary>
    /// 清除控制器上所有 SETASG 寄存器映射
    /// 重连后或切换数据表前调用，避免残留映射导致地址冲突
    /// </summary>
    public void ClearRAssignment()
    {
        _client.SendCommand("CLRASG");
    }

    /// <inheritdoc cref="ClearRAssignment" />
    public Task<bool> ClearRAssignmentAsync()
    {
        return System.Threading.Tasks.Task.Run(() =>
        {
            ClearRAssignment();
            return true;
        });
    }

    /// <summary>清除控制器上报警</summary>
    /// <param name="type">报警类型（可选），例如：1=当前报警</param>
    public void ClearAlarm(int type = 0)
    {
        // type<=0 发送 "CLRALM"，type>0 发送 "CLRALM {type}"
        _client.SendCommand(type > 0 ? $"CLRALM {type}" : "CLRALM");
    }

    /// <inheritdoc cref="ClearAlarm(int)" />
    public Task<bool> ClearAlarmAsync(int type = 0)
    {
        return System.Threading.Tasks.Task.Run(() =>
        {
            ClearAlarm(type);
            return true;
        });
    }

    /// <summary>设置 SNPX 客户端 ID。</summary>
    /// <param name="clientId">客户端 ID（默认 1024）。</param>
    public void SetClientId(int clientId)
    {
        _config.ClientId = clientId;
        _client.SetClientId(clientId);
    }

    /// <summary>释放资源，断开连接。</summary>
    public void Dispose()
    {
        _client.Dispose();
    }
}
