using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class TransferBenchmarks : BenchmarkBase
{
    private TransferReadRepository _repository = null!;

    [IterationSetup]
    public override void IterationSetup()
    {
        base.IterationSetup();
        _repository = new TransferReadRepository(context: Context);
    }

    [Benchmark]
    public async Task GetByIdAsync()
        => await _repository.GetByIdAsync(transferId: Db.TransferId);

    [Benchmark]
    public async Task GetAllAsync()
        => await _repository.GetAllAsync(userId: Db.UserId, pageSize: RowCount);

    [Benchmark]
    public async Task GetAllAsync_WithAccountFilter()
        => await _repository.GetAllAsync(userId: Db.UserId, accountId: Db.FromAccountId, pageSize: RowCount);

    [Benchmark]
    public async Task GetAllAsync_WithCursor() => await _repository.GetAllAsync(
        userId: Db.UserId,
        cursorOccurredAt: DateTimeOffset.UtcNow.AddDays(days: -30),
        cursorId: Guid.NewGuid(),
        pageSize: RowCount
    );

    [Benchmark]
    public async Task GetAllAsync_LastMonth() => await _repository.GetAllAsync(
        userId: Db.UserId,
        dateFrom: DateTimeOffset.UtcNow.AddDays(days: -30),
        pageSize: RowCount
    );

    [Benchmark]
    public async Task GetAllAsync_AccountAndDate() => await _repository.GetAllAsync(
        userId: Db.UserId,
        accountId: Db.FromAccountId,
        dateFrom: DateTimeOffset.UtcNow.AddDays(days: -90),
        pageSize: RowCount
    );

    [Benchmark]
    public async Task GetPendingRateAsync()
        => await _repository.GetPendingRateAsync();

    [Benchmark]
    public async Task GetPendingCreditCountAsync()
        => await _repository.GetPendingCreditCountAsync(gracePeriod: TimeSpan.FromMinutes(value: 5));
}