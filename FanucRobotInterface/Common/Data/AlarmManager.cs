using System;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>报警管理器。</summary>
/// </summary>
public class AlarmManager
{
    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;

    internal AlarmManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    /// <summary>每条报警记录占用的字数（由 mode 决定）。</summary>
    private static int GetRecordSize(AlarmMessageMode mode)
    {
        return mode switch
        {
            AlarmMessageMode.Short => 51,
            AlarmMessageMode.Medium => 91,
            _ => 100  // Full
        };
    }

    /// <summary>报警系统变量名（由 type 决定）。注意：List→ALM[E1]、Current→ALM[1]、Password→ALM[P1]。</summary>
    private static string GetAlarmVariable(AlarmType type)
    {
        return type switch
        {
            AlarmType.Current => "ALM[1]",
            AlarmType.Password => "ALM[P1]",
            _ => "ALM[E1]"  // List
        };
    }

    /// <summary>构造 SETASG 命令字符串。</summary>
    private static string BuildCommand(int address, AlarmType type, AlarmMessageMode mode, int totalSize, int recordSize)
    {
        string varName = GetAlarmVariable(type);
        // Full 模式无 @1.{recordSize} 后缀
        if (mode == AlarmMessageMode.Full)
        {
            return $"SETASG {address} {totalSize} {varName} 1";
        }
        return $"SETASG {address} {totalSize} {varName}@1.{recordSize} 1";
    }

    /// <summary>同步读取报警列表/当前报警。</summary>
    /// <param name="count">读取数量（最多 n 条）。</param>
    /// <param name="type">报警类型（List=ALM[E1] 报警历史，Current=ALM[1] 当前报警，Password=ALM[P1] 密码报警）。</param>
    /// <param name="mode">报警消息模式（Full=完整，Short=简短，Medium=中等）。</param>
    /// <returns>报警信息数组。</returns>
    public AlarmItem[] Read(int count, AlarmType type = AlarmType.List, AlarmMessageMode mode = AlarmMessageMode.Full)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "报警数量必须大于 0");
        }

        int recordSize = GetRecordSize(mode);
        int totalSize = recordSize * count;

        // 报警使用固定临时槽位（预留地址 1）
        int address = _allocator.CentralTempSlotAddress;
        string command = BuildCommand(address, type, mode, totalSize, recordSize);
        _client.SendCommand(command);

        var words = _client.ReadRegisters(address, totalSize);
        return ParseAlarms(words, count, recordSize, mode);
    }

    /// <summary>异步读取报警列表/当前报警。</summary>
    /// <param name="count">读取数量（最多 n 条）。</param>
    /// <param name="type">报警类型（List=ALM[E1] 报警历史，Current=ALM[1] 当前报警，Password=ALM[P1] 密码报警）。</param>
    /// <param name="mode">报警消息模式（Full=完整，Short=简短，Medium=中等）。</param>
    /// <returns>报警信息数组。</returns>
    public Task<AlarmItem[]> ReadAsync(int count, AlarmType type = AlarmType.List, AlarmMessageMode mode = AlarmMessageMode.Full)
        => Task.Run(() => Read(count, type, mode));

    /// <summary>解析报警数据。每条记录 recordSize 字。</summary>
    private static AlarmItem[] ParseAlarms(short[] data, int count, int recordSize, AlarmMessageMode mode)
    {
        var result = new AlarmItem[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * recordSize;
            var item = new AlarmItem
            {
                AlarmId = data[offset + 0],
                AlarmNumber = data[offset + 1],
                CauseAlarmId = data[offset + 2],
                CauseAlarmNumber = data[offset + 3],
                Severity = data[offset + 4],
                Year = data[offset + 5],
                Month = data[offset + 6],
                Day = data[offset + 7],
                Hour = data[offset + 8],
                Minute = data[offset + 9],
                Second = data[offset + 10]
            };

            // 字符串字段：AlarmMessage 在 Medium/Full，CauseAlarmMessage/SeverityMessage 仅 Full
            if (mode != AlarmMessageMode.Short)
            {
                item.AlarmMessage = ShortsToString(data, offset + 11, 40);
            }
            if (mode == AlarmMessageMode.Full)
            {
                item.CauseAlarmMessage = ShortsToString(data, offset + 51, 40);
                item.SeverityMessage = ShortsToString(data, offset + 91, 5);
            }

            result[i] = item;
        }
        return result;
    }

    /// <summary>把 wordCount 个字（小端 short→字节）解码为字符串，截断到首个 NUL。</summary>
    /// <remarks>
    /// 使用 SnpxProtocol.DefaultStringEncoding（net8.0+ 默认 GBK）而非 Encoding.Default，
    /// 与 SnpxProtocol.StringEncoding 保持一致。.NET 5+ 的 Encoding.Default 在非 Windows 平台
    /// 返回 UTF-8，与 Windows 中文 FANUC 控制器不兼容，会导致中文报警消息乱码。
    /// </remarks>
    private static string ShortsToString(short[] data, int start, int wordCount)
    {
        var bytes = new byte[wordCount * 2];
        for (int i = 0; i < wordCount; i++)
        {
            bytes[i * 2] = (byte)(data[start + i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((data[start + i] >> 8) & 0xFF);
        }

        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }

        return SnpxProtocol.DefaultStringEncoding.GetString(bytes).TrimEnd('\0');
    }
}
