using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FanucRobotInterface.Common.Data;

/// <summary>
/// 系统变量组（用于批量读写）
/// </summary>
public class SystemVariableGroup
{
    private readonly SystemVariablesManager _manager;
    private readonly List<VariableInfo> _variables;
    private readonly int _baseAddress;
    private readonly int _totalSize;
    private readonly bool _isPaged;

    internal SystemVariableGroup(SystemVariablesManager manager, List<VariableInfo> variables, bool isPaged = false)
    {
        _manager = manager;
        _variables = variables;
        _isPaged = isPaged;
        _baseAddress = variables.Count > 0 ? variables[0].Address : 0;
        _totalSize = 0;
        foreach (var v in variables)
        {
            _totalSize += v.Size;
        }
    }

    /// <summary>批量读取变量组中的所有变量值。</summary>
    /// <returns>按变量声明顺序返回的对象数组。</returns>
    public object[] Read()
    {
        var words = _manager.ReadRaw(_baseAddress, _totalSize);
        var result = new object[_variables.Count];
        int offset = 0;
        for (int i = 0; i < _variables.Count; i++)
        {
            var v = _variables[i];
            var slice = new short[v.Size];
            Array.Copy(words, offset, slice, 0, v.Size);
            result[i] = _manager.ConvertToValue(slice, v.Type);
            offset += v.Size;
        }
        return result;
    }

    /// <summary>异步批量读取变量组中的所有变量值。</summary>
    /// <returns>按变量声明顺序返回的对象数组。</returns>
    public Task<object[]> ReadAsync() => Task.Run(Read);

    /// <summary>批量写入变量组中的所有变量值。</summary>
    /// <param name="values">按变量声明顺序传入的值数组（长度必须与变量数一致）。</param>
    public void Write(object[] values)
    {
        if (values.Length != _variables.Count)
        {
            throw new ArgumentException($"Expected {_variables.Count} values, got {values.Length}");
        }

        var words = new short[_totalSize];
        int offset = 0;
        for (int i = 0; i < _variables.Count; i++)
        {
            var v = _variables[i];
            var slice = _manager.ConvertFromValue(values[i], v.Type);
            Array.Copy(slice, 0, words, offset, slice.Length);
            offset += v.Size;
        }

        _manager.WriteRaw(_baseAddress, words);
    }

    /// <summary>异步批量写入变量组中的所有变量值。</summary>
    /// <param name="values">按变量声明顺序传入的值数组（长度必须与变量数一致）。</param>
    public Task WriteAsync(object[] values) => Task.Run(() => Write(values));
}
