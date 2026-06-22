using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Category;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Single-lookup Category methods that don't take a page-size parameter — split out from
/// <see cref="CategoryBenchmarks"/> so each runs once per benchmark suite instead of 4 redundant times.
/// </summary>
public class CategoryLookupBenchmarks : BenchmarkBase
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
	public async Task GetTotalByCategoryAsync()
		=> await _totalRepository.GetByCategoryAsync(userId: Db.UserId, categoryId: Db.CategoryId, period: new DateOnly(year: 2025, month: 1, day: 1));

	[Benchmark]
	public async Task GetAllTotalsByPeriodAsync()
		=> await _totalRepository.GetAllByPeriodAsync(userId: Db.UserId, period: new DateOnly(year: 2025, month: 1, day: 1));
}