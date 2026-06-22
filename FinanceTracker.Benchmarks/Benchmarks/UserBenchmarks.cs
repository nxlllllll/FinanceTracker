using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Infrastructure.Database.Repositories.User;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class UserBenchmarks : PaginatedBenchmarkBase
{
	private UserReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new UserReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetHistoryAsync_All()
		=> await _repository.GetHistoryAsync(userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetHistoryAsync_IncomeOnly()
		=> await _repository.GetHistoryAsync(userId: Db.UserId, type: OperationFilterType.Income, pageSize: PageSize);

	[Benchmark]
	public async Task GetHistoryAsync_TransferOnly()
		=> await _repository.GetHistoryAsync(userId: Db.UserId, type: OperationFilterType.Transfer, pageSize: PageSize);

	[Benchmark]
	public async Task GetHistoryAsync_WithCursor() => await _repository.GetHistoryAsync(
		userId: Db.UserId,
		cursorOccurredAt: DateTimeOffset.UtcNow.AddDays(days: -30),
		cursorId: Guid.NewGuid(),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetHistoryAsync_LastMonth() => await _repository.GetHistoryAsync(
		userId: Db.UserId,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -30),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetHistoryAsync_IncomeLastQuarter() => await _repository.GetHistoryAsync(
		userId: Db.UserId,
		type: OperationFilterType.Income,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -90),
		pageSize: PageSize
	);
}