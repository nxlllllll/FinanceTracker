using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Services;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class CurrencyConversionServiceTests
{
	private ICurrencyRateReadRepository _currencyRateReadRepository = null!;
	private CurrencyConversionService _service = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyRateReadRepository = Substitute.For<ICurrencyRateReadRepository>();
		_service = new CurrencyConversionService(currencyRateReadRepository: _currencyRateReadRepository);
	}
	
	[Test]
    public async Task GetConversionRateAsync_WhenSameCurrency_ShouldReturnRateOneWithoutPending()
    {
        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: "RUB",
            toCurrency: "RUB",
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 1m);
        await Assert.That(value: result.IsPending).IsFalse();

        await _currencyRateReadRepository.DidNotReceive().GetRateAsync(
            baseCurrencyCode: Arg.Any<string>(),
            targetCurrencyCode: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task GetConversionRateAsync_WhenExactRateExists_ShouldReturnRateWithoutPending()
    {
        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 90m);

        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: "USD",
            toCurrency: "RUB",
            date: date
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 90m);
        await Assert.That(value: result.IsPending).IsFalse();
    }

    [Test]
    public async Task GetConversionRateAsync_WhenExactRateNotExists_ShouldReturnLatestRateWithPending()
    {
        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        _currencyRateReadRepository.GetLatestRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 85m);

        ConversionResult result = await _service.GetConversionRateAsync(
            fromCurrency: "USD",
            toCurrency: "RUB",
            date: date
        );

        await Assert.That(value: result.Rate).IsEqualTo(expected: 85m);
        await Assert.That(value: result.IsPending).IsTrue();
    }

    [Test]
    public async Task GetConversionRateAsync_WhenNoRateExists_ShouldThrowCurrencyRateNotFoundException()
    {
        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            date: date,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        _currencyRateReadRepository.GetLatestRateAsync(
            baseCurrencyCode: "USD",
            targetCurrencyCode: "RUB",
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (decimal?)null);

        await Assert.That(action: async () => await _service.GetConversionRateAsync(
            fromCurrency: "USD",
            toCurrency: "RUB",
            date: date
        )).Throws<CurrencyRateNotFoundException>();
    }
}