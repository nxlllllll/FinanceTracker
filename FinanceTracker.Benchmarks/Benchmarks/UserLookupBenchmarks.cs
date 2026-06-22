using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Single-lookup User methods that don't take a page-size parameter — split out from
/// <see cref="UserBenchmarks"/> so each runs once per benchmark suite instead of 4 redundant times.
/// Also covers <see cref="IUserAuthRepository.GetByIdAsync"/> and <see cref="IUserQueryRepository.GetByIdAsync"/> —
/// same method name, two different interfaces explicitly implemented by UserReadRepository, so each
/// is called through its own interface-typed field to disambiguate.
/// </summary>
public class UserLookupBenchmarks : BenchmarkBase
{
    private UserReadRepository _repository = null!;
    private IUserAuthRepository _authRepository = null!;
    private IUserQueryRepository _queryRepository = null!;
    private UserSessionReadRepository _sessionRepository = null!;

    [IterationSetup]
    public override void IterationSetup()
    {
        base.IterationSetup();
        _repository = new UserReadRepository(context: Context);
        _authRepository = _repository;
        _queryRepository = _repository;
        _sessionRepository = new UserSessionReadRepository(context: Context);
    }

    [Benchmark]
    public async Task GetByEmailAsync()
        => await _repository.GetByEmailAsync(email: "user1@bench.com");

    [Benchmark]
    public async Task GetByIdAsync_Auth()
        => await _authRepository.GetByIdAsync(userId: Db.UserId);

    [Benchmark]
    public async Task GetByIdAsync_Query()
        => await _queryRepository.GetByIdAsync(userId: Db.UserId);

    [Benchmark]
    public async Task GetTotalBalanceAsync() => await _repository.GetTotalBalanceAsync(
        userId: Db.UserId,
        baseCurrency: Currency.Reconstitute(value: "RUB"),
        date: DateOnly.FromDateTime(dateTime: DateTime.UtcNow)
    );

    [Benchmark]
    public async Task GetIncomeExpenseSummaryAsync() => await _repository.GetIncomeExpenseSummaryAsync(
        userId: Db.UserId,
        period: new DateOnly(year: DateTime.UtcNow.Year, month: DateTime.UtcNow.Month, day: 1)
    );

    [Benchmark]
    public async Task GetHistoryAsync_FirstPage()
        => await _repository.GetHistoryAsync(userId: Db.UserId, pageSize: 20);

    [Benchmark]
    public async Task GetSessionByRefreshTokenAsync()
        => await _sessionRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: Db.RefreshTokenHash);
}