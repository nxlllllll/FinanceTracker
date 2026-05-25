using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Currency;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class CurrencyConversionServiceTests
{
	private ICurrencyRateReadRepository _currencyRateReadRepository = null!;
	private CurrencyConversionService _service = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyRateReadRepository = Substitute.For<ICurrencyRateReadRepository>();
		_service = new CurrencyConversionService(
            currencyRateReadRepository: _currencyRateReadRepository,
            logger: Substitute.For<ILogger<CurrencyConversionService>>()
        );
	}
	
	[Test]
    public async Task GetConversionRateAsync_WhenSameCurrency_ShouldReturnRateOneWithoutPending()
    {
        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: Currency.Create(value: "RUB").Value,
            toCurrency: Currency.Create(value: "RUB").Value,
            date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 1m);
        await Assert.That(value: result.IsPending).IsFalse();

        await _currencyRateReadRepository.DidNotReceive().GetRateAsync(
            baseCurrencyCode: Arg.Any<Currency>(),
            targetCurrencyCode: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task GetConversionRateAsync_WhenExactRateExists_ShouldReturnRateWithoutPending()
    {
        DateOnly date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Currency.Create(value: "USD").Value,
            targetCurrencyCode: Currency.Create(value: "RUB").Value,
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 90m);

        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: Currency.Create(value: "USD").Value,
            toCurrency: Currency.Create(value: "RUB").Value,
            date: date
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 90m);
        await Assert.That(value: result.IsPending).IsFalse();
    }

    [Test]
    public async Task GetConversionRateAsync_WhenExactRateNotExists_ShouldReturnLatestRateWithPending()
    {
        DateOnly date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Currency.Create(value: "USD").Value,
            targetCurrencyCode: Currency.Create(value: "RUB").Value,
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        _currencyRateReadRepository.GetLatestRateAsync(
            baseCurrencyCode: Currency.Create(value: "USD").Value,
            targetCurrencyCode: Currency.Create(value: "RUB").Value,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 85m);

        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: Currency.Create(value: "USD").Value,
            toCurrency: Currency.Create(value: "RUB").Value,
            date: date
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 85m);
        await Assert.That(value: result.IsPending).IsTrue();
    }

    [Test]
    public async Task GetConversionRateAsync_WhenNoRateExists_ShouldThrowCurrencyRateNotFoundException()
    {
        DateOnly date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Currency.Create(value: "USD").Value,
            targetCurrencyCode: Currency.Create(value: "RUB").Value,
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        _currencyRateReadRepository.GetLatestRateAsync(
            baseCurrencyCode: Currency.Create(value: "USD").Value,
            targetCurrencyCode: Currency.Create(value: "RUB").Value,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        await Assert.That(action: async () => await _service.GetConversionRateAsync(
            fromCurrency: Currency.Create(value: "USD").Value,
            toCurrency: Currency.Create(value: "RUB").Value,
            date: date
        )).Throws<CurrencyRateNotFoundException>();
    }
}
