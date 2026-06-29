using BenchmarkDotNet.Attributes;
using FinanceTracker.Benchmarks.Infrastructure;
using FinanceTracker.Infrastructure.Database.Context;

namespace FinanceTracker.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[HtmlExporter]
public abstract class BenchmarkBase
{
	protected const int PageSize = 20;

	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected BenchmarkDatabase Db => BenchmarkDatabase.Instance;

	public virtual void IterationSetup()
		=> Context = Db.CreateContext();

	[IterationCleanup]
	public void IterationCleanup()
		=> Context.DisposeAsync().AsTask().GetAwaiter().GetResult();
}