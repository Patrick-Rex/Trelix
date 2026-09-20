using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Trelix.Server.Persistence;

// INTEGER UTC ticks retain precision and allow SQLite comparisons and ordering.
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    value => new DateTimeOffset(value, TimeSpan.Zero));
