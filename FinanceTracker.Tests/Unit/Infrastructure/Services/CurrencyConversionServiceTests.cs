using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Tests.Unit.Helpers;
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
			dateProvider: FakeDateProvider.Default,
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
		await Assert.That(value: result.Status).IsEqualTo(expected: RateStatus.Exact);

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
		await Assert.That(value: result.Status).IsEqualTo(expected: RateStatus.Exact);
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
		await Assert.That(value: result.Status).IsEqualTo(expected: RateStatus.Pending);
	}

	[Test]
	public async Task GetConversionRateAsync_WhenNoRateExists_ShouldThrowCurrencyRateMissingException()
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
		)).Throws<CurrencyRateMissingException>();
	}

	[Test]
	public async Task GetStableRateAsync_WhenRateIsKnown_ShouldReturnRate()
	{
		DateTimeOffset asOf = DateTimeOffset.UtcNow;

		_currencyRateReadRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Currency.Create(value: "USD").Value,
			targetCurrencyCode: Currency.Create(value: "RUB").Value,
			asOf: asOf,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		decimal result = await _service.GetStableRateAsync(
			fromCurrency: Currency.Create(value: "USD").Value,
			toCurrency: Currency.Create(value: "RUB").Value,
			asOf: asOf
		);

		await Assert.That(value: result).IsEqualTo(expected: 90m);
	}

	[Test]
	public async Task GetStableRateAsync_WhenRateNotKnown_ShouldThrowCurrencyRateMissingException()
	{
		_currencyRateReadRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		await Assert.That(action: async () => await _service.GetStableRateAsync(
			fromCurrency: Currency.Create(value: "USD").Value,
			toCurrency: Currency.Create(value: "RUB").Value,
			asOf: DateTimeOffset.UtcNow
		)).Throws<CurrencyRateMissingException>();
	}

	[Test]
	public async Task GetStableRatesBatchAsync_WhenEmpty_ShouldReturnEmptyDictionary()
	{
		Dictionary<CurrencyStableRateRequest, decimal> result = await _service.GetStableRatesBatchAsync(requests: []);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetStableRatesBatchAsync_WhenAllResolved_ShouldReturnAllRates()
	{
		CurrencyStableRateRequest request = new CurrencyStableRateRequest(
			From: Currency.Create(value: "USD").Value,
			To: Currency.Create(value: "RUB").Value,
			AsOf: DateTimeOffset.UtcNow
		);

		_currencyRateReadRepository.GetRatesKnownAtOrBeforeBatchAsync(
			requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new Dictionary<CurrencyStableRateRequest, decimal> { [request] = 90m });

		Dictionary<CurrencyStableRateRequest, decimal> result = await _service.GetStableRatesBatchAsync(requests: [request]);

		await Assert.That(value: result[request]).IsEqualTo(expected: 90m);
	}

	[Test]
	public async Task GetStableRatesBatchAsync_WhenRequestMissingFromRepositoryResult_ShouldThrowCurrencyRateMissingException()
	{
		CurrencyStableRateRequest request = new CurrencyStableRateRequest(
			From: Currency.Create(value: "USD").Value,
			To: Currency.Create(value: "RUB").Value,
			AsOf: DateTimeOffset.UtcNow
		);

		_currencyRateReadRepository.GetRatesKnownAtOrBeforeBatchAsync(
			requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new Dictionary<CurrencyStableRateRequest, decimal>());

		await Assert.That(
			action: async () => await _service.GetStableRatesBatchAsync(requests: [request])
		).Throws<CurrencyRateMissingException>();
	}
}
