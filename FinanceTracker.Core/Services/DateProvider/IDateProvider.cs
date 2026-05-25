namespace FinanceTracker.Core.Services.DateProvider;

public interface IDateProvider
{
	DateTimeOffset UtcNow { get; }
	DateOnly UtcToday { get; }
}
