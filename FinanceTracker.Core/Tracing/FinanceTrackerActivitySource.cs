using System.Diagnostics;

namespace FinanceTracker.Core.Tracing;

public static class FinanceTrackerActivitySource
{
	public const string Name = "FinanceTracker";
	public static readonly ActivitySource Instance = new ActivitySource(name: Name);
}
