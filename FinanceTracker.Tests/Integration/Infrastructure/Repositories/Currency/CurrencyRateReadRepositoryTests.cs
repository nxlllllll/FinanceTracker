using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Currency;

public sealed class CurrencyRateReadRepositoryTests : DatabaseFixture
{
	private CurrencyRateReadRepository _readRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
    {
        _readRepository = new CurrencyRateReadRepository(context: Context);
        _currencyBuilder = new CurrencyBuilder(context: Context);
    }

    private async Task SeedRateAsync(
        Core.ValueObjects.Currency baseCode,
        Core.ValueObjects.Currency targetCode,
        decimal rate,
        DateOnly date)
    {
        await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity()
        {
            BaseCode = baseCode,
            TargetCode = targetCode,
            Rate = rate,
            ActualAt = date,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetRateAsync_WhenSameCurrency_ShouldReturnOne()
    {
        decimal? result = await _readRepository.GetRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
        );

        await Assert.That(value: result).IsEqualTo(expected: 1m);
    }

    [Test]
    public async Task GetRateAsync_WhenRateExists_ShouldReturnRate()
    {
        await _currencyBuilder.CreateAsync(code: "USD");
        await _currencyBuilder.CreateAsync(code: "RUB");

        DateOnly date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        await SeedRateAsync(
            baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value, 
            targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value, 
            rate: 90m, 
            date: date
        );

        decimal? result = await _readRepository.GetRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            date: date
        );

        await Assert.That(value: result).IsEqualTo(expected: 90m);
    }

    [Test]
    public async Task GetRateAsync_WhenRateNotExists_ShouldReturnNull()
    {
        decimal? result = await _readRepository.GetRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
        );

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetLatestRateAsync_WhenSameCurrency_ShouldReturnOne()
    {
        decimal? result = await _readRepository.GetLatestRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
        );

        await Assert.That(value: result).IsEqualTo(expected: 1m);
    }

    [Test]
    public async Task GetLatestRateAsync_WhenMultipleRatesExist_ShouldReturnLatest()
    {
        await _currencyBuilder.CreateAsync(code: "USD");
        await _currencyBuilder.CreateAsync(code: "RUB");

        await SeedRateAsync(
            baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value, 
            targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            rate: 85m,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(days: -2).UtcDateTime)
        );
        await SeedRateAsync(
            baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value, 
            targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            rate: 90m,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(days: -1).UtcDateTime)
        );
        await SeedRateAsync(
            baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value, 
            targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            rate: 92m,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
        );

        decimal? result = await _readRepository.GetLatestRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
        );

        await Assert.That(value: result).IsEqualTo(expected: 92m);
    }

    [Test]
    public async Task GetLatestRateAsync_WhenNoRateExists_ShouldReturnNull()
    {
        decimal? result = await _readRepository.GetLatestRateAsync(
            baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
            targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
        );

        await Assert.That(value: result).IsNull();
    }
}
