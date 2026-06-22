using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class RecurringTransactionBenchmarks : PaginatedBenchmarkBase
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
}