using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// <summary>系统变量管理器。</summary>
/// </summary>
public class SystemVariablesManager
{
    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<string, VariableInfo> _cache = new();

    internal SystemVariablesManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    // ---- 变量组 ----

    /// <summary>
    /// 创建变量组。
    /// 空间够 → 连续分配 + 永久缓存（批量 READ）
    /// 空间不够 → 自动分页模式（通过临时槽位逐变量读写）
    /// </summary>
    public SystemVariableGroup CreateVariableGroup(List<(string variableName, VariableType type)> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            throw new ArgumentException("Variable list cannot be empty", nameof(variables));
        }

        var infos = new List<VariableInfo>(variables.Count);
        foreach (var (variableName, type) in variables)
        {
            // 复用已绑定的变量（缓存命中不发 SETASG），未绑定的才分配+绑定
            var (address, size) = BindVariable(variableName, type);
            infos.Add(new VariableInfo
            {
                Name = variableName,
                Type = type,
                Size = size,
                Address = address
            });
        }

        return new SystemVariableGroup(this, infos);
    }

    // ---- 单个变量读写 ----

    /// <summary>读取系统变量（整数类型，同步）。</summary>
    /// <param name="variableName">系统变量名，如 $SCR_GRP[1].$MSTERPOS。</param>
    /// <returns>32 位符号整数值。</returns>
    public int ReadInt(string variableName) => ReadSingleInt(variableName);

    /// <summary>异步读取系统变量（整数类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>32 位符号整数值。</returns>
    public Task<int> ReadIntAsync(string variableName) => Task.Run(() => ReadInt(variableName));

    /// <summary>写入系统变量（整数类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">32 位符号整数值。</param>
    public void WriteInt(string variableName, int value) => WriteSingleInt(variableName, value);

    /// <summary>异步写入系统变量（整数类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">32 位符号整数值。</param>
    public Task WriteIntAsync(string variableName, int value) => Task.Run(() => WriteInt(variableName, value));

    /// <summary>读取系统变量（布尔类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>true=ON/非零，false=OFF/零。</returns>
    public bool ReadBool(string variableName) => ReadSingleBool(variableName);

    /// <summary>异步读取系统变量（布尔类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>true=ON/非零，false=OFF/零。</returns>
    public Task<bool> ReadBoolAsync(string variableName) => Task.Run(() => ReadBool(variableName));

    /// <summary>写入系统变量（布尔类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">true=写入 1，false=写入 0。</param>
    public void WriteBool(string variableName, bool value) => WriteSingleBool(variableName, value);

    /// <summary>异步写入系统变量（布尔类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">true=写入 1，false=写入 0。</param>
    public Task WriteBoolAsync(string variableName, bool value) => Task.Run(() => WriteBool(variableName, value));

    /// <summary>读取系统变量（浮点数类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>32 位浮点数值。</returns>
    public float ReadFloat(string variableName) => ReadSingleFloat(variableName);

    /// <summary>异步读取系统变量（浮点数类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>32 位浮点数值。</returns>
    public Task<float> ReadFloatAsync(string variableName) => Task.Run(() => ReadFloat(variableName));

    /// <summary>写入系统变量（浮点数类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">32 位浮点数值。</param>
    public void WriteFloat(string variableName, float value) => WriteSingleFloat(variableName, value);

    /// <summary>异步写入系统变量（浮点数类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">32 位浮点数值。</param>
    public Task WriteFloatAsync(string variableName, float value) => Task.Run(() => WriteFloat(variableName, value));

    /// <summary>读取系统变量（字符串类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>字符串内容。</returns>
    public string ReadString(string variableName) => ReadSingleString(variableName);

    /// <summary>异步读取系统变量（字符串类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>字符串内容。</returns>
    public Task<string> ReadStringAsync(string variableName) => Task.Run(() => ReadString(variableName));

    /// <summary>写入系统变量（字符串类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">要写入的字符串。</param>
    public void WriteString(string variableName, string value) => WriteSingleString(variableName, value);

    /// <summary>异步写入系统变量（字符串类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">要写入的字符串。</param>
    public Task WriteStringAsync(string variableName, string value) => Task.Run(() => WriteString(variableName, value));

    /// <summary>读取系统变量（位置数据类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>完整位置信息（关节坐标、笛卡尔坐标、配置）。</returns>
    public PositionInfo ReadPosition(string variableName) => ReadSinglePosition(variableName);

    /// <summary>异步读取系统变量（位置数据类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <returns>完整位置信息（关节坐标、笛卡尔坐标、配置）。</returns>
    public Task<PositionInfo> ReadPositionAsync(string variableName) => Task.Run(() => ReadPosition(variableName));

    /// <summary>写入系统变量（位置数据类型，同步）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">完整位置信息。</param>
    public void WritePosition(string variableName, PositionInfo value) => WriteSinglePosition(variableName, value);

    /// <summary>异步写入系统变量（位置数据类型）。</summary>
    /// <param name="variableName">系统变量名。</param>
    /// <param name="value">完整位置信息。</param>
    public Task WritePositionAsync(string variableName, PositionInfo value) => Task.Run(() => WritePosition(variableName, value));

    // ---- 内部：供 SystemVariableGroup 使用 ----

    internal short[] ReadRaw(int address, int words)
        => _client.ReadRegisters(address, words);

    internal bool WriteRaw(int address, short[] words)
    {
        _client.WriteRegisters(address, words);
        return true;
    }

    internal object ConvertToValue(short[] words, VariableType type)
    {
        return type switch
        {
            VariableType.INT => words.Length > 0 ? (object)(int)words[0] : 0,
            VariableType.REAL => words.Length >= 2 ? ShortsToFloat(words, 0) : 0f,
            VariableType.STRING => ShortsToString(words),
            VariableType.POS => ShortsToPosition(words),
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
    }

    internal short[] ConvertFromValue(object value, VariableType type)
    {
        return type switch
        {
            VariableType.INT => new[] { (short)(value is int i ? i : 0) },
            VariableType.REAL => FloatToShorts(value is float f ? f : 0f),
            VariableType.STRING => StringToShorts(value?.ToString() ?? string.Empty),
            VariableType.POS => PositionToShorts(value as PositionInfo),
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
    }

    // ---- 单个变量实现 ----

    private (int address, int words) BindVariable(string variableName, VariableType type)
    {
        // 已绑定则复用缓存地址，不再发 SETASG
        if (_cache.TryGetValue(variableName, out var cached))
        {
            return (cached.Address, cached.Size);
        }

        int size = GetTypeSize(type);
        int address = _allocator.Allocate(size);
        // fmt 依类型：INT="1", REAL="0", STRING="1", POS="0.0"
        string fmt = type switch
        {
            VariableType.REAL => "0",
            VariableType.POS => "0.0",
            _ => "1"
        };
        string command = $"SETASG {address} {size} {variableName} {fmt}";
        _client.SendCommand(command);

        _cache[variableName] = new VariableInfo { Name = variableName, Type = type, Size = size, Address = address };
        return (address, size);
    }

    private int ReadSingleInt(string variableName)
    {
        var (address, _) = BindVariable(variableName, VariableType.INT);
        var words = _client.ReadRegisters(address, 2);
        return words.Length >= 2 ? (words[1] << 16) | (words[0] & 0xFFFF) : 0;
    }

    private void WriteSingleInt(string variableName, int value)
    {
        var (address, _) = BindVariable(variableName, VariableType.INT);
        _client.WriteRegisters(address, new[] { (short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF) });
    }

    private bool ReadSingleBool(string variableName)
    {
        var (address, _) = BindVariable(variableName, VariableType.INT);
        var words = _client.ReadRegisters(address, 2);
        return words.Length > 0 && words[0] != 0;
    }

    private void WriteSingleBool(string variableName, bool value)
    {
        var (address, _) = BindVariable(variableName, VariableType.INT);
        _client.WriteRegisters(address, new[] { (short)(value ? 1 : 0), (short)0 });
    }

    private float ReadSingleFloat(string variableName)
    {
        var (address, _) = BindVariable(variableName, VariableType.REAL);
        var words = _client.ReadRegisters(address, 2);
        return ShortsToFloat(words, 0);
    }

    private void WriteSingleFloat(string variableName, float value)
    {
        var (address, _) = BindVariable(variableName, VariableType.REAL);
        _client.WriteRegisters(address, FloatToShorts(value));
    }

    private string ReadSingleString(string variableName)
    {
        var (address, _) = BindVariable(variableName, VariableType.STRING);
        var words = _client.ReadRegisters(address, 40);
        return ShortsToString(words);
    }

    private void WriteSingleString(string variableName, string value)
    {
        var (address, _) = BindVariable(variableName, VariableType.STRING);
        _client.WriteRegisters(address, StringToShorts(value ?? string.Empty));
    }

    private PositionInfo ReadSinglePosition(string variableName)
    {
        var (address, _) = BindVariable(variableName, VariableType.POS);
        var words = _client.ReadRegisters(address, 50);
        return ShortsToPosition(words);
    }

    private void WriteSinglePosition(string variableName, PositionInfo value)
    {
        var (address, _) = BindVariable(variableName, VariableType.POS);
        _client.WriteRegisters(address, PositionToShorts(value));
    }

    // ---- 转换辅助 ----

    private static int GetTypeSize(VariableType type)
    {
        return type switch
        {
            VariableType.INT => 2,
            VariableType.REAL => 2,
            VariableType.STRING => 40,
            VariableType.POS => 50,
            _ => 1
        };
    }

    private static float ShortsToFloat(short[] words, int index)
    {
        if (words.Length < index + 2)
        {
            return 0f;
        }
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), words[index]);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), words[index + 1]);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static short[] FloatToShorts(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new[]
        {
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0)),
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2))
        };
    }

    private static string ShortsToString(short[] words)
    {
        var bytes = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            bytes[i * 2] = (byte)(words[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((words[i] >> 8) & 0xFF);
        }
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
        {
            Array.Resize(ref bytes, nullIndex);
        }
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    private static short[] StringToShorts(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        var words = new short[40];
        for (int i = 0; i < 40; i++)
        {
            words[i] = (short)((i * 2 < bytes.Length ? bytes[i * 2] : 0)
                             | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
        return words;
    }

    private static PositionInfo ShortsToPosition(short[] words)
    {
        var result = new PositionInfo();
        if (words.Length < 12)
        {
            return result;
        }
        result.Cartesian.X = ShortsToFloat(words, 0);
        result.Cartesian.Y = ShortsToFloat(words, 2);
        result.Cartesian.Z = ShortsToFloat(words, 4);
        result.Cartesian.W = ShortsToFloat(words, 6);
        result.Cartesian.P = ShortsToFloat(words, 8);
        result.Cartesian.R = ShortsToFloat(words, 10);
        return result;
    }

    private static short[] PositionToShorts(PositionInfo value)
    {
        var words = new short[12];
        if (value?.Cartesian != null)
        {
            CopyFloat(words, 0, value.Cartesian.X);
            CopyFloat(words, 2, value.Cartesian.Y);
            CopyFloat(words, 4, value.Cartesian.Z);
            CopyFloat(words, 6, value.Cartesian.W);
            CopyFloat(words, 8, value.Cartesian.P);
            CopyFloat(words, 10, value.Cartesian.R);
        }
        return words;
    }

    private static void CopyFloat(short[] words, int index, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        words[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0));
        words[index + 1] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
    }
}
