using BenchmarkDotNet.Loggers;

namespace FinanceTracker.Benchmarks.Logging;

/// <summary>Suppresses BenchmarkDotNet's default verbose console output.</summary>
public sealed class CleanConsoleLogger : ILogger
{
	public static readonly CleanConsoleLogger Instance = new CleanConsoleLogger();

	public string Id => nameof(CleanConsoleLogger);
	public int Priority => 0;

	public void Write(LogKind logKind, string text) { }
	public void WriteLine(LogKind logKind, string text) { }
	public void WriteLine() { }
	public void Flush() { }
}
