using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Infrastructure.Services.Date;

public sealed class DateProvider : IDateProvider
{
	public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
	public DateOnly UtcToday => DateOnly.FromDateTime(dateTime: UtcNow.UtcDateTime);
}
