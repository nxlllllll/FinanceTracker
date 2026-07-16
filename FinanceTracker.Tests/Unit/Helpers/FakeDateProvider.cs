using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class FakeDateProvider(DateTimeOffset utcNow) : IDateProvider
{
	public DateTimeOffset UtcNow { get; } = utcNow;
	public DateOnly UtcToday => DateOnly.FromDateTime(dateTime: UtcNow.UtcDateTime);

	public static FakeDateProvider Default => new FakeDateProvider(utcNow: new DateTimeOffset(year: 2024, month: 1, day: 15, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero));
}
