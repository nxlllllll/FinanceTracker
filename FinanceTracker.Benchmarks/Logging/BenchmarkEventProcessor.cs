using BenchmarkDotNet.EventProcessors;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace FinanceTracker.Benchmarks.Logging;

/// <summary>
/// Drives <see cref="BenchmarkConsoleReporter"/> from BenchmarkDotNet's 
/// own public progress hooks instead of parsing its console log output
/// </summary>
public sealed class BenchmarkEventProcessor : EventProcessor
{
	public static readonly BenchmarkEventProcessor Instance = new BenchmarkEventProcessor();

	public override void OnStartRunBenchmarksInType(Type type, IReadOnlyList<BenchmarkCase> benchmarks)
		=> BenchmarkConsoleReporter.Instance.OnClassTotalKnown(total: benchmarks.Count);

	public override void OnStartRunBenchmark(BenchmarkCase benchmarkCase)
	{
		string className = benchmarkCase.Descriptor.Type.Name.Replace(oldValue: "Benchmarks", newValue: String.Empty);
		string method = benchmarkCase.Descriptor.WorkloadMethod.Name;

		BenchmarkConsoleReporter.Instance.OnBenchmarkStarted(className: className, method: method, rowCount: String.Empty);
	}

	public override void OnEndRunBenchmark(BenchmarkCase benchmarkCase, BenchmarkReport report)
		=> BenchmarkConsoleReporter.Instance.OnBenchmarkDone();

	public override void OnEndRunBenchmarksInType(Type type, Summary summary)
	{
		int passed = summary.Reports.Count(predicate: r => r is { Success: true, ResultStatistics: not null });
		int failed = summary.Reports.Length - passed;

		BenchmarkConsoleReporter.Instance.OnClassEnd(passed: passed, failed: failed);
	}
}