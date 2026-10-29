namespace Trelix.Core.Abstractions;

/// <summary>提供由持久化操作维护的乐观并发基准。</summary>
public interface IConcurrencyStamped
{
    /// <summary>用于比较和刷新资源状态的并发标记。</summary>
    Guid ConcurrencyStamp { get; set; }
}
