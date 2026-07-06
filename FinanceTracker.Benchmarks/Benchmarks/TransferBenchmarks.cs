using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Infrastructure.Services.Date;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class TransferBenchmarks : BenchmarkBase
{
	private TransferReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new TransferReadRepository(
			context: Context,
			dateProvider: new DateProvider()
		);
	}

	[Benchmark]
	public async Task GetAllAsync()
		=> await _repository.GetAllAsync(userId: Db.UserId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_WithAccountFilter()
		=> await _repository.GetAllAsync(userId: Db.UserId, accountId: Db.FromAccountId, pageSize: PageSize);

	[Benchmark]
	public async Task GetAllAsync_WithCursor() => await _repository.GetAllAsync(
		userId: Db.UserId,
		cursorOccurredAt: DateTimeOffset.UtcNow.AddDays(days: -30),
		cursorId: Guid.NewGuid(),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_LastMonth() => await _repository.GetAllAsync(
		userId: Db.UserId,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -30),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetAllAsync_AccountAndDate() => await _repository.GetAllAsync(
		userId: Db.UserId,
		accountId: Db.FromAccountId,
		dateFrom: DateTimeOffset.UtcNow.AddDays(days: -90),
		pageSize: PageSize
	);

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _repository.GetByIdAsync(transferId: Db.TransferId);

	[Benchmark]
	public async Task GetPendingRateAsync()
		=> await _repository.GetPendingRateAsync();

	[Benchmark]
	public async Task GetPendingCreditCountAsync()
		=> await _repository.GetPendingCreditCountAsync(gracePeriod: TimeSpan.FromMinutes(value: 5));

	[Benchmark]
	public async Task GetPendingCreditForCompensationAsync()
		=> await _repository.GetPendingCreditForCompensationAsync(compensationThreshold: TimeSpan.FromMinutes(value: 30));
}
