using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Single-lookup Transaction methods that don't take a page-size parameter — split out from
/// <see cref="TransactionBenchmarks"/> so each runs once per benchmark suite instead of 4 redundant times.
/// </summary>
public class TransactionLookupBenchmarks : BenchmarkBase
{
	private TransactionReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new TransactionReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _repository.GetByIdAsync(transactionId: Db.TransactionId, userId: Db.UserId);

	[Benchmark]
	public async Task GetPendingRateAsync()
		=> await _repository.GetPendingRateAsync();
}