using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class FakeDateProvider(DateTime utcNow) : IDateProvider
{
	public DateTime UtcNow { get; } = utcNow;
	public DateOnly UtcToday => DateOnly.FromDateTime(dateTime: UtcNow);
 
	public static FakeDateProvider Default => new FakeDateProvider(utcNow: new DateTime(year: 2024, month: 1, day: 15, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc));
}