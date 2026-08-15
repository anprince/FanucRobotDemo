using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FanucRobotInterface.Common;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// 注释管理器
/// 用于读取和写入机器人各信号/寄存器的用户注释（标签/名称）
/// 格式: {prefix}[C{index}]，例如 SI[C1]、DI[C5]、DO[C3]
/// 每条注释占用 40 words（80 字节）
/// 支持类型（共 20 种），见 <see cref="CommentType" />。
/// SETASG: "SETASG {addr} 40 {prefix}[C{index}] 1"
/// </summary>
public class CommentManager
{
    /// <summary>
    /// 注释类型（共 20 种）
    /// 枚举名与 SNPX SETASG 前缀完全一致，可直接 .ToString() 使用
    /// </summary>
    public enum CommentType
    {
        /// <summary>数字输入信号注释（DI）</summary>
        DI,
        /// <summary>数字输出信号注释（DO）</summary>
        DO,
        /// <summary>机器人输入信号注释（RI）</summary>
        RI,
        /// <summary>机器人输出信号注释（RO）</summary>
        RO,
        /// <summary>用户输入信号注释（UI）</summary>
        UI,
        /// <summary>用户输出信号注释（UO）</summary>
        UO,
        /// <summary>信号输入注释（SI）</summary>
        SI,
        /// <summary>信号输出注释（SO）</summary>
        SO,
        /// <summary>组输入信号注释（GI）</summary>
        GI,
        /// <summary>组输出信号注释（GO）</summary>
        GO,
        /// <summary>模拟输入信号注释（AI）</summary>
        AI,
        /// <summary>模拟输出信号注释（AO）</summary>
        AO,
        /// <summary>焊接输入信号注释（WI）</summary>
        WI,
        /// <summary>焊接输出信号注释（WO）</summary>
        WO,
        /// <summary>焊接系统输入信号注释（WSI）</summary>
        WSI,
        /// <summary>焊接系统输出信号注释（WSO）</summary>
        WSO,
        /// <summary>字符串寄存器注释（SR）</summary>
        SR,
        /// <summary>数值寄存器注释（R）</summary>
        R,
        /// <summary>位置寄存器注释（PR）</summary>
        PR,
        /// <summary>标志寄存器注释（F）</summary>
        F
    }

    private const int CommentWords = 40;  // 注释固定 40 字（80 字节）

    private readonly SnpxProtocol _client;
    private readonly RegisterMap _allocator;
    private readonly Dictionary<(string, int), int> _cache = new();

    internal CommentManager(SnpxProtocol client, RegisterMap allocator)
    {
        _client = client;
        _allocator = allocator;
    }

    // ---- Read ----

    /// <summary>通过字符串前缀读取注释。</summary>
    /// <param name="prefix">注释前缀，支持全部 20 种：SI、SO、DI、DO、RI、RO、UI、UO、GI、GO、AI、AO、WI、WO、WSI、WSO、SR、R、PR、F</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <returns>注释内容。</returns>
    public string Read(string prefix, int index) => ReadComment(prefix, index);

    /// <summary>通过字符串前缀异步读取注释。</summary>
    /// <param name="prefix">注释前缀，支持全部 20 种：SI、SO、DI、DO、RI、RO、UI、UO、GI、GO、AI、AO、WI、WO、WSI、WSO、SR、R、PR、F</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <returns>注释内容。</returns>
    public Task<string> ReadAsync(string prefix, int index) => Task.Run(() => Read(prefix, index));

    /// <summary>通过 <see cref="CommentType" /> 读取注释。</summary>
    /// <param name="type">注释类型。</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <returns>注释内容。</returns>
    public string Read(CommentType type, int index) => ReadComment(GetPrefix(type), index);

    /// <summary>通过 <see cref="CommentType" /> 异步读取注释。</summary>
    /// <param name="type">注释类型。</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <returns>注释内容。</returns>
    public Task<string> ReadAsync(CommentType type, int index) => Task.Run(() => Read(type, index));

    // ---- Write ----

    /// <summary>通过字符串前缀写入注释。</summary>
    /// <param name="prefix">注释前缀，支持全部 20 种：SI、SO、DI、DO、RI、RO、UI、UO、GI、GO、AI、AO、WI、WO、WSI、WSO、SR、R、PR、F</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <param name="value">注释内容，超过 80 字节将被截断</param>
    public void Write(string prefix, int index, string value) => WriteComment(prefix, index, value);

    /// <summary>通过字符串前缀异步写入注释。</summary>
    /// <param name="prefix">注释前缀，支持全部 20 种：SI、SO、DI、DO、RI、RO、UI、UO、GI、GO、AI、AO、WI、WO、WSI、WSO、SR、R、PR、F</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <param name="value">注释内容，超过 80 字节将被截断</param>
    public Task WriteAsync(string prefix, int index, string value) => Task.Run(() => Write(prefix, index, value));

    /// <summary>通过 <see cref="CommentType" /> 写入注释。</summary>
    /// <param name="type">注释类型。</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <param name="value">注释内容，超过 80 字节将被截断</param>
    public void Write(CommentType type, int index, string value) => WriteComment(GetPrefix(type), index, value);

    /// <summary>通过 <see cref="CommentType" /> 异步写入注释。</summary>
    /// <param name="type">注释类型。</param>
    /// <param name="index">索引（SI/SO 从 0 开始，其余从 1 开始）</param>
    /// <param name="value">注释内容，超过 80 字节将被截断</param>
    public Task WriteAsync(CommentType type, int index, string value) => Task.Run(() => Write(type, index, value));

    // ---- 内部 ----

    private string ReadComment(string prefix, int index)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("前缀不能为空", nameof(prefix));
        }

        int address = GetOrBindAddress(prefix, index);
        var words = _client.ReadRegisters(address, CommentWords);
        return ShortsToString(words, 0, CommentWords);
    }

    private void WriteComment(string prefix, int index, string value)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("前缀不能为空", nameof(prefix));
        }

        int address = GetOrBindAddress(prefix, index);
        var words = StringToShorts(value ?? string.Empty, CommentWords);
        _client.WriteRegisters(address, words);
    }

    private int GetOrBindAddress(string prefix, int index)
    {
        var key = (prefix, index);
        if (_cache.TryGetValue(key, out var address))
        {
            return address;
        }

        address = _allocator.Allocate(CommentWords);
        string command = $"SETASG {address} {CommentWords} {prefix}[C{index}] 1";
        _client.SendCommand(command);
        _cache[key] = address;
        return address;
    }

    private static string GetPrefix(CommentType type) => type.ToString();

    private string ShortsToString(short[] data, int start, int wordCount)
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

        // 使用协议配置的 StringEncoding。
        // 默认是 GBK(936)（中文 FANUC 控制器注释常用编码）；如控制器用 shift_jis/windows-1252 等，
        // 需在调用前设置 robot.StringEncoding = Encoding.GetEncoding(...)。
        return _client.StringEncoding.GetString(bytes).TrimEnd('\0');
    }

    private short[] StringToShorts(string value, int wordCount)
    {
        var bytes = _client.StringEncoding.GetBytes(value);
        var result = new short[wordCount];
        for (int i = 0; i < wordCount; i++)
        {
            result[i] = (short)((i * 2 < bytes.Length ? bytes[i * 2] : 0)
                               | ((i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0) << 8));
        }
        return result;
    }
}
