using System;
using System.Threading.Tasks;
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
		=> await _repository.GetByUserIdAsync(userId: Db.UserId, pageSize: RowCount);

	[Benchmark]
	public async Task GetDueTodayAsync()
	{
		DateTime now = DateTime.UtcNow;
		await _repository.GetDueTodayAsync(
			dayOfMonth: now.Day,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
	}
}