namespace FinanceTracker.Core.Services.DateProvider;

/// <summary>
/// Abstracts the system clock to allow deterministic time in tests.
/// Inject this instead of calling <see cref="DateTimeOffset.UtcNow"/> directly.
/// </summary>
public interface IDateProvider
{
	/// <summary>Current UTC date and time.</summary>
	DateTimeOffset UtcNow { get; }

	/// <summary>Current UTC date without time component.</summary>
	DateOnly UtcToday { get; }
}
