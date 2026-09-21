using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Trelix.Server.Persistence;

// INTEGER UTC ticks retain precision and allow SQLite comparisons and ordering.
/// <summary>在 DateTimeOffset 与 UTC ticks 间转换，保留精度并支持 SQLite 比较排序。</summary>
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    value => new DateTimeOffset(value, TimeSpan.Zero));
