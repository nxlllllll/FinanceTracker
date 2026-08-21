using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class RecurringTransactionBenchmarks : BenchmarkBase
{
	private RecurringTransactionReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new RecurringTransactionReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetByUserIdAsync()
		=> await _repository.GetByUserIdAsync(userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _repository.GetByIdAsync(recurringTransactionId: Db.RecurringTransactionId, userId: Db.UserId);

	[Benchmark]
	public async Task GetDueAsync()
		=> await _repository.GetDueAsync(asOf: DateTimeOffset.UtcNow);

	[Benchmark]
	public async Task GetOverdueAsync()
		=> await _repository.GetOverdueAsync(before: DateTimeOffset.UtcNow.AddDays(days: -1));
}
