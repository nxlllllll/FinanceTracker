using System.Diagnostics;

namespace FinanceTracker.Core.Services.Tracing;

public static class FinanceTrackerActivitySource
{
	public const string Name = "FinanceTracker";
	public static readonly ActivitySource Instance = new ActivitySource(name: Name);
}
