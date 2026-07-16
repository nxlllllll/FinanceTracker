using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class TransactionBenchmarks : BenchmarkBase
{
	private TransactionReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new TransactionReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetAllAsync()
		=> await _repository.GetAllAsync(userId: Db.UserId, accountId: Db.AccountId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_WithCursor() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		cursorOccurredAt: DateTimeOffset.UtcNow.AddDays(days: -30),
		cursorId: Guid.NewGuid(),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_DebitOnly() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		direction: DirectionType.Debit,
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_ByCategory() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		categoryId: Db.ExpenseCategoryId,
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_LastMonth() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -30),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_Combined() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		direction: DirectionType.Debit,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -90),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_NotExcluded() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.AccountId,
		isExcluded: false,
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetByIdAsync() => await _repository.GetByIdAsync(
		transactionId: Db.TransactionId,
		userId: Db.UserId
	);

	[Benchmark]
	public async Task GetPendingRateAsync()
		=> await _repository.GetPendingRateAsync(batchSize: PageSize);
}
