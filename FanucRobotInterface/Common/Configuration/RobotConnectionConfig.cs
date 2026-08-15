namespace FanucRobotInterface.Common.Configuration;

/// <summary>
/// FANUC 机器人 SNPX 连接配置。
/// 指定 IP 地址、端口号、超时时间及客户端标识。
/// </summary>
public class RobotConnectionConfig
{
    /// <summary>IP 地址（默认 127.0.0.1）</summary>
    public string IpAddress { get; set; } = "127.0.0.1";

    /// <summary>端口号（默认 PacketConstants.DefaultPort）</summary>
    public int Port { get; set; } = 60008;

    /// <summary>连接超时时间（毫秒，默认 10000）</summary>
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>读写超时时间（毫秒，默认 PacketConstants.ReadWriteTimeoutMs）</summary>
    public int ReadWriteTimeoutMs { get; set; } = 10000;

    /// <summary>客户端标识（默认 1024）</summary>
    public int ClientId { get; set; } = 1024;

    /// <summary>获取默认配置实例</summary>
    public static RobotConnectionConfig Default => new RobotConnectionConfig();
}
