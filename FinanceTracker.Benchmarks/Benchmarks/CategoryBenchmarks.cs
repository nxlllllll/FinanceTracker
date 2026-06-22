using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Category;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class CategoryBenchmarks : PaginatedBenchmarkBase
{
	private CategoryReadRepository _readRepository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_readRepository = new CategoryReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetAllAsync()
		=> await _readRepository.GetAllAsync( userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_ExpenseOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, type: CategoryType.Expense, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_ActiveOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, isArchived: false, pageSize: PageSize);
}