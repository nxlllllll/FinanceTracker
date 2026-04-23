using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public sealed class CurrencyRateRepositoryTests : DatabaseFixture
{
	private CurrencyRateRepository _repository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
        => _repository = new CurrencyRateRepository(context: Context);

    private async Task SeedRateAsync(
        string baseCode,
        string targetCode,
        decimal rate,
        DateOnly date)
    {
        await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity()
        {
            BaseCode = baseCode,
            TargetCode = targetCode,
            Rate = rate,
            ActualAt = date,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetRateAsync_WhenSameCurrency_ShouldReturnOne()
    {
        decimal? result = await _repository.GetRateAsync(
            baseCurrencyCode: "RUB",
            targetCurrencyCode: "RUB",
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        await Assert.That(value: result).IsEqualTo(expected: 1m);
    }

    [Test]
    public async Task GetRateAsync_WhenRateExists_ShouldReturnRate()
    {
        await CreateCurrencyAsync(code: "USD");
        await CreateCurrencyAsync(code: "RUB");

        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedRateAsync(baseCode: "USD", targetCode: "RUB", rate: 90m, date: date);

        decimal? result = await _repository.GetRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            date: date
        );

        await Assert.That(value: result).IsEqualTo(expected: 90m);
    }

    [Test]
    public async Task GetRateAsync_WhenRateNotExists_ShouldReturnNull()
    {
        decimal? result = await _repository.GetRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetLatestRateAsync_WhenSameCurrency_ShouldReturnOne()
    {
        decimal? result = await _repository.GetLatestRateAsync(
            baseCurrencyCode: "RUB",
            targetCurrencyCode: "RUB"
        );

        await Assert.That(value: result).IsEqualTo(expected: 1m);
    }

    [Test]
    public async Task GetLatestRateAsync_WhenMultipleRatesExist_ShouldReturnLatest()
    {
        await CreateCurrencyAsync(code: "USD");
        await CreateCurrencyAsync(code: "RUB");

        await SeedRateAsync(
            baseCode: "USD", 
            targetCode: "RUB",
            rate: 85m,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(value: -2))
        );
        await SeedRateAsync(
            baseCode: "USD", 
            targetCode: "RUB",
            rate: 90m,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(value: -1))
        );
        await SeedRateAsync(
            baseCode: "USD", 
            targetCode: "RUB",
            rate: 92m,
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        decimal? result = await _repository.GetLatestRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB"
        );

        await Assert.That(value: result).IsEqualTo(expected: 92m);
    }

    [Test]
    public async Task GetLatestRateAsync_WhenNoRateExists_ShouldReturnNull()
    {
        decimal? result = await _repository.GetLatestRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB"
        );

        await Assert.That(value: result).IsNull();
    }
}