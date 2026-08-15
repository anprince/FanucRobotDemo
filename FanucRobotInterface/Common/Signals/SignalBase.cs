using System.Threading.Tasks;

namespace FanucRobotInterface.Common.Signals;

/// <summary>
/// 信号基类。
/// </summary>
public abstract class SignalBase<T>
{
    /// <summary>信号类别。</summary>
    public SignalCategory Category { get; protected set; }

    /// <summary>读取单个信号值（同步）。</summary>
    public abstract T ReadSingle(int index);

    /// <summary>批量读取信号值（同步）。</summary>
    public abstract T[] Read(int startIndex, int count);

    /// <summary>写入单个信号值（同步）。</summary>
    public abstract bool WriteSingle(int index, T value);

    /// <summary>批量写入信号值（同步）。</summary>
    public abstract bool Write(int startIndex, T[] values);

    /// <summary>异步读取单个信号值。</summary>
    public abstract Task<T> ReadSingleAsync(int index);

    /// <summary>异步批量读取信号值。</summary>
    public abstract Task<T[]> ReadAsync(int startIndex, int count);

    /// <summary>异步写入单个信号值。</summary>
    public abstract Task<bool> WriteSingleAsync(int index, T value);

    /// <summary>异步批量写入信号值。</summary>
    public abstract Task<bool> WriteAsync(int startIndex, T[] values);

    /// <summary>受保护的构造函数。</summary>
    protected SignalBase()
    {
    }
}
