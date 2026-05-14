using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Infrastructure.Services.Date;

public sealed class DateProvider : IDateProvider
{
	public DateTime UtcNow => DateTime.UtcNow;
	public DateOnly UtcToday => DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
}