using BenchmarkDotNet.Attributes;
using FinanceTracker.Benchmarks.Infrastructure;
using FinanceTracker.Infrastructure.Database.Context;

namespace FinanceTracker.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[HtmlExporter]
public abstract class BenchmarkBase
{
	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected BenchmarkDatabase Db => BenchmarkDatabase.Instance;

	[Params(1_000, 10_000, 100_000, 1_000_000)]
	public int RowCount { get; set; }

	public virtual void IterationSetup()
		=> Context = Db.CreateContext();

	[IterationCleanup]
	public void IterationCleanup()
		=> Context.DisposeAsync().AsTask().GetAwaiter().GetResult();
}