using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class ControllableDateProvider(DateTimeOffset initial) : IDateProvider
{
	public DateTimeOffset UtcNow { get; private set; } = initial;

	public DateOnly UtcToday { get; }

	public void Advance(TimeSpan by)
		=> UtcNow += by;
}