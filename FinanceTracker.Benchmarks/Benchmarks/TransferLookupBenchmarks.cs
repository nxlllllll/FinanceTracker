using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Single-lookup Transfer methods that don't take a page-size parameter — split out from
/// <see cref="TransferBenchmarks"/> so each runs once per benchmark suite instead of 4 redundant times.
/// </summary>
public class TransferLookupBenchmarks : BenchmarkBase
{
	private TransferReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new TransferReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _repository.GetByIdAsync(transferId: Db.TransferId);

	[Benchmark]
	public async Task GetPendingRateAsync()
		=> await _repository.GetPendingRateAsync();

	[Benchmark]
	public async Task GetPendingCreditCountAsync()
		=> await _repository.GetPendingCreditCountAsync(gracePeriod: TimeSpan.FromMinutes(value: 5));

	[Benchmark]
	public async Task GetPendingCreditForCompensationAsync()
		=> await _repository.GetPendingCreditForCompensationAsync(compensationThreshold: TimeSpan.FromMinutes(value: 30));
}