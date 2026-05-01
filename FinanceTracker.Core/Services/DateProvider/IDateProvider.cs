namespace FinanceTracker.Core.Services.DateProvider;

public interface IDateProvider
{
	DateTime UtcNow { get; }
	DateOnly UtcToday { get; }
}