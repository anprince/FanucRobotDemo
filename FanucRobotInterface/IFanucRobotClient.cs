using System;
using System.Threading.Tasks;
using FanucRobotInterface.Common.Data;
using FanucRobotInterface.Common.Signals;

namespace FanucRobotInterface;

/// <summary>
/// FANUC 机器人客户端核心接口。
/// 通过 SNPX 协议与 FANUC 机器人控制器通信，提供信号管理、数据管理、报警管理和系统变量访问等功能。
/// 所有操作均有同步和异步两种版本，SNPX 协议为串行通信，同一实例不支持并发请求。
/// </summary>
public interface IFanucRobotClient : IDisposable
{
    // ---- 管理器 ----

    /// <summary>位置数据管理器（关节/笛卡尔坐标，PR 读写）。</summary>
    PositionDataManager Position { get; }

    /// <summary>系统变量管理器（$VAR 读写，支持整数/浮点/字符串/位置类型）。</summary>
    SystemVariablesManager SystemVariables { get; }

    /// <summary>数值寄存器管理器（R[i] 读写）。</summary>
    NumRegManager NumReg { get; }

    /// <summary>位置寄存器管理器（PR[i] 读写，支持关节/笛卡尔坐标及批量操作）。</summary>
    PosRegManager PosReg { get; }

    /// <summary>字符串寄存器管理器（SR[i] 读写）。</summary>
    StrRegManager StrReg { get; }

    /// <summary>标志寄存器管理器（F[i] 读写）。</summary>
    FlagManager Flag { get; }

    /// <summary>任务状态管理器（PRG[i] 任务监控）。</summary>
    TaskManager Task { get; }

    /// <summary>报警管理器（ALM[i] 报警历史/当前报警读取）。</summary>
    AlarmManager Alarm { get; }

    /// <summary>注释管理器（COMMENT[i] 读取）。</summary>
    CommentManager Comment { get; }

    // ---- 信号 ----

    /// <summary>数字输入信号 DI[1]~DI[n]。true=ON，false=OFF。</summary>
    DigitalSignal DI { get; }

    /// <summary>数字输出信号 DO[1]~DO[n]。true=ON，false=OFF。</summary>
    DigitalSignal DO { get; }

    /// <summary>机器人输入信号 RI[1]~RI[n]。true=ON，false=OFF。</summary>
    DigitalSignal RI { get; }

    /// <summary>机器人输出信号 RO[1]~RO[n]。true=ON，false=OFF。</summary>
    DigitalSignal RO { get; }

    /// <summary>用户输入信号 UI[1]~UI[n]。true=ON，false=OFF。</summary>
    DigitalSignal UI { get; }

    /// <summary>用户输出信号 UO[1]~UO[n]。true=ON，false=OFF。</summary>
    DigitalSignal UO { get; }

    /// <summary>操作员输入信号 SI[1]~SI[n]。true=ON，false=OFF。</summary>
    DigitalSignal SI { get; }

    /// <summary>操作员输出信号 SO[1]~SO[n]。true=ON，false=OFF。</summary>
    DigitalSignal SO { get; }

    /// <summary>焊接输入信号 WI[1]~WI[n]。true=ON，false=OFF。</summary>
    DigitalSignal WI { get; }

    /// <summary>焊接输出信号 WO[1]~WO[n]。true=ON，false=OFF。</summary>
    DigitalSignal WO { get; }

    /// <summary>焊接状态输入信号 WSI[1]~WSI[n]。true=ON，false=OFF。</summary>
    DigitalSignal WSI { get; }

    /// <summary>焊接状态输出信号 WSO[1]~WSO[n]。true=ON，false=OFF。</summary>
    DigitalSignal WSO { get; }

    /// <summary>组输入信号 GI[1]~GI[n]（16 位无符号，0~65535）。</summary>
    GroupSignal GI { get; }

    /// <summary>组输出信号 GO[1]~GO[n]（16 位无符号，0~65535）。</summary>
    GroupSignal GO { get; }

    /// <summary>模拟输入信号 AI[1]~AI[n]（32 位有符号整数）。</summary>
    AnalogSignal AI { get; }

    /// <summary>模拟输出信号 AO[1]~AO[n]（32 位有符号整数）。</summary>
    AnalogSignal AO { get; }

    /// <summary>PMC 信号管理器（R 区/K 区/D 区及 PMCR2 参数）。</summary>
    PmcSignal Pmc { get; }

    // ---- 连接 ----

    /// <summary>是否已连接到机器人控制器。</summary>
    bool IsConnected { get; }

    /// <summary>连接被对端关闭/网络异常导致断开时触发（后台线程触发，订阅者需 marshal 回 UI 线程）。</summary>
    event Action ConnectionLost;

    /// <summary>连接到指定 IP 和端口的机器人控制器（同步）。</summary>
    /// <param name="ipAddress">机器人控制器 IP 地址。</param>
    /// <param name="port">SNPX 端口号，默认 60008。</param>
    /// <returns>连接成功返回 true。</returns>
    bool Connect(string ipAddress, int port = 60008);

    /// <summary>使用默认配置连接到机器人控制器（同步）。</summary>
    /// <returns>连接成功返回 true。</returns>
    bool Connect();

    /// <summary>异步连接到指定 IP 和端口的机器人控制器。</summary>
    /// <param name="ipAddress">机器人控制器 IP 地址。</param>
    /// <param name="port">SNPX 端口号，默认 60008。</param>
    /// <returns>连接成功返回 true。</returns>
    Task<bool> ConnectAsync(string ipAddress, int port = 60008);

    /// <summary>使用默认配置异步连接到机器人控制器。</summary>
    /// <returns>连接成功返回 true。</returns>
    Task<bool> ConnectAsync();

    /// <summary>断开与机器人控制器的连接。</summary>
    void Disconnect();

    // ---- 其他 ----

    /// <summary>清空 R 寄存器映射缓存（用于重新分配），首次连接默认行为，谨慎使用。</summary>
    void ClearRAssignment();

    /// <summary>异步清空 R 寄存器映射缓存（用于重新分配），首次连接默认行为，谨慎使用。</summary>
    /// <returns>操作成功返回 true。</returns>
    Task<bool> ClearRAssignmentAsync();

    /// <summary>清除报警。</summary>
    /// <param name="type">报警类型（0 表示全部清除）。</param>
    void ClearAlarm(int type = 0);

    /// <summary>异步清除报警。</summary>
    /// <param name="type">报警类型（0 表示全部清除）。</param>
    /// <returns>操作成功返回 true。</returns>
    Task<bool> ClearAlarmAsync(int type = 0);

    /// <summary>设置 SNPX 客户端 ID。</summary>
    /// <param name="clientId">客户端 ID（默认 1024）。</param>
    void SetClientId(int clientId);
}
