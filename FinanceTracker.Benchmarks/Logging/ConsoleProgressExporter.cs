using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace FinanceTracker.Benchmarks.Logging;

/// <summary>
/// It is called after completion of each class.
/// Closes the class row with the final results.
/// </summary>
public sealed class ConsoleProgressExporter : IExporter
{
	public static readonly ConsoleProgressExporter Instance = new ConsoleProgressExporter();

	public string Name => nameof(ConsoleProgressExporter);

	public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
	{
		int passed = summary.Reports.Count(predicate: r => r is { Success: true, ResultStatistics: not null });
		int failed = summary.Reports.Length - passed;

		BenchmarkConsoleReporter.Instance.OnClassEnd(passed: passed, failed: failed);
		return [];
	}

	public void ExportToLog(Summary summary, ILogger logger) { }

	public void Finish() { }
}
