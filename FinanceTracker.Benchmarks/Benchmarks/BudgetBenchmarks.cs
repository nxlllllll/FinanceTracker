using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class BudgetBenchmarks : BenchmarkBase
{
	private BudgetReadRepository _readRepository = null!;
	private BudgetProgressReadRepository _progressRepository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_readRepository = new BudgetReadRepository(context: Context);
		_progressRepository = new BudgetProgressReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetAllAsync()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_ActiveOnly()
		=> await _readRepository.GetAllAsync(userId: Db.UserId, isActive: true, pageSize: PageSize);

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _readRepository.GetByIdAsync(budgetId: Db.BudgetId, userId: Db.UserId);

	[Benchmark]
	public async Task GetActiveByCategoryAsync()
		=> await _readRepository.GetActiveByCategoryAsync(userId: Db.UserId, categoryId: Db.CategoryId, date: new DateOnly(year: 2025, month: 6, day: 15));

	[Benchmark]
	public async Task GetProgressByBudgetIdAsync()
		=> await _progressRepository.GetByBudgetIdAsync(budgetId: Db.BudgetId, userId: Db.UserId);

	[Benchmark]
	public async Task HasOverlappingAsync() => await _readRepository.HasOverlappingAsync(
		userId: Db.UserId,
		categoryId: Db.CategoryId,
		from: DateOnly.FromDateTime(dateTime: DateTime.UtcNow.AddDays(value: -5)),
		to: DateOnly.FromDateTime(dateTime: DateTime.UtcNow.AddDays(value: 5))
	);
}