using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class BudgetBenchmarks : PaginatedBenchmarkBase
{
	private BudgetReadRepository _readRepository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_readRepository = new BudgetReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetAllAsync()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_ActiveOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, isActive: true, pageSize: PageSize);
}