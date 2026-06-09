using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Category;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class CategoryBenchmarks : BenchmarkBase
{
	private CategoryReadRepository _readRepository = null!;
	private CategoryTotalReadRepository _totalRepository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_readRepository = new CategoryReadRepository(context: Context);
		_totalRepository = new CategoryTotalReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _readRepository.GetByIdAsync(categoryId: Db.CategoryId, userId: Db.UserId);

	[Benchmark]
	public async Task GetAllAsync()
		=> await _readRepository.GetAllAsync( userId: Db.UserId, pageSize: RowCount);

	[Benchmark]
	public async Task GetAllAsync_ExpenseOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, type: CategoryType.Expense, pageSize: RowCount);

	[Benchmark]
	public async Task GetAllAsync_ActiveOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, isArchived: false, pageSize: RowCount);

	[Benchmark]
	public async Task GetTotalByCategoryAsync()
		=> await _totalRepository.GetByCategoryAsync(userId: Db.UserId, categoryId: Db.CategoryId, period: new DateOnly(year: 2025, month: 1, day: 1));

	[Benchmark]
	public async Task GetAllTotalsByPeriodAsync()
		=> await _totalRepository.GetAllByPeriodAsync(userId: Db.UserId, period: new DateOnly(year: 2025, month: 1, day: 1));
}