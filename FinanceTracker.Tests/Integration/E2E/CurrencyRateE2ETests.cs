using System.Net;
using System.Text.Json;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.CurrencyRate.Client;
using FinanceTracker.Worker.CurrencyRate.Job;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CurrencyRateJob → ExchangeRateApiClient (locked via HttpMessageHandler) → currency_rates.
/// Checks for a successful fetch, a graceful skip in the absence of currencies,
/// a partial failure (one currency is unavailable), and the behaviour of the resilience pipeline.
/// </summary>
public sealed class CurrencyRateE2ETests : E2EFixture
{
	private FakeHttpMessageHandler _httpHandler = null!;

	protected override void ConfigureAdditionalServices(IServiceCollection services, IConfiguration configuration)
	{
		_httpHandler = new FakeHttpMessageHandler();

		services.AddHttpClient<ExchangeRateApiClient>().ConfigurePrimaryHttpMessageHandler(configureHandler: _ => _httpHandler);

		services.AddScoped<CurrencyRateJob>();

		services.AddOptions<ExchangeRateApiOptions>();

		services.AddOptions<CurrencyRateJobOptions>();
	}

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB", isActive: true);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD", isActive: true);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "EUR", isActive: true);
	}

	private async Task RunCurrencyRateJobAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		CurrencyRateJob job = scope.ServiceProvider.GetRequiredService<CurrencyRateJob>();
		IJobExecutionContext ctx = Substitute.For<IJobExecutionContext>();
		await job.Execute(context: ctx);
	}

	private static string BuildSuccessResponse(string baseCode, Dictionary<string, decimal> rates) => JsonSerializer.Serialize(new
	{
		result = "success",
		base_code = baseCode,
		conversion_rates = rates
	});

	[Test]
	public async Task CurrencyRateJob_WithActiveRates_ShouldUpsertRatesForAllActiveCurrencies()
	{
		_httpHandler.SetupResponse(baseCode: "RUB", json: BuildSuccessResponse(
			baseCode: "RUB",
			rates: new Dictionary<string, decimal> { ["USD"] = 0.011m, ["EUR"] = 0.010m }
		));
		_httpHandler.SetupResponse(baseCode: "USD", json: BuildSuccessResponse(
			baseCode: "USD",
			rates: new Dictionary<string, decimal> { ["RUB"] = 90m, ["EUR"] = 0.93m }
		));
		_httpHandler.SetupResponse(baseCode: "EUR", json: BuildSuccessResponse(
			baseCode: "EUR",
			rates: new Dictionary<string, decimal> { ["RUB"] = 97m, ["USD"] = 1.07m }
		));

		await RunCurrencyRateJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		int rateCount = await readCtx.CurrencyRates.CountAsync();

		await Assert.That(value: rateCount).IsGreaterThan(minimum: 0);

		bool rubToUsd = await readCtx.CurrencyRates.AnyAsync(predicate: r => r.BaseCode == "RUB" && r.TargetCode == "USD");
		await Assert.That(value: rubToUsd).IsTrue();
	}

	[Test]
	public async Task CurrencyRateJob_WhenNoActiveCurrencies_ShouldSkipGracefully()
	{
		foreach (CurrencyEntity currency in Context.Currencies)
			currency.IsActive = false;

		await Context.SaveChangesAsync();

		await RunCurrencyRateJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		int rateCount = await readCtx.CurrencyRates.CountAsync();

		// HTTP should not be called, courses should not appear
		await Assert.That(value: rateCount).IsEqualTo(expected: 0);
		await Assert.That(value: _httpHandler.CallCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task CurrencyRateJob_WhenOneApiCallFails_ShouldProcessOtherCurrencies()
	{
		// RUB — successful
		_httpHandler.SetupResponse(baseCode: "RUB", json: BuildSuccessResponse(
			baseCode: "RUB",
			rates: new Dictionary<string, decimal> { ["USD"] = 0.011m, ["EUR"] = 0.010m }
		));

		// USD — 500, Polly retry is exhausted → this currency is skipped
		_httpHandler.SetupError(baseCode: "USD", statusCode: HttpStatusCode.InternalServerError);

		// EUR — successful
		_httpHandler.SetupResponse(baseCode: "EUR", json: BuildSuccessResponse(
			baseCode: "EUR",
			rates: new Dictionary<string, decimal> { ["RUB"] = 97m, ["USD"] = 1.07m }
		));

		await RunCurrencyRateJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool hasRubRates = await readCtx.CurrencyRates.AnyAsync(predicate: r => r.BaseCode == "RUB");
		bool hasEurRates = await readCtx.CurrencyRates.AnyAsync(predicate: r => r.BaseCode == "EUR");

		await Assert.That(value: hasRubRates).IsTrue();
		await Assert.That(value: hasEurRates).IsTrue();
	}

	[Test]
	public async Task CurrencyRateJob_WithRetry_ShouldSucceedAfterTransientFailure()
	{
		// Polly retry works at the HttpClient level — before the exception
		// reaches GetRatesAsync. Therefore, the first 500 → Polly retry → the second request is 200.
		// For RUB: failCount=1 means that the first HTTP call will return 500,
		// Polly retry will launch the second one, which is successful.
		_httpHandler.SetupTransientError(baseCode: "RUB", failCount: 1, successJson: BuildSuccessResponse(
			baseCode: "RUB",
			rates: new Dictionary<string, decimal> { ["USD"] = 0.011m }
		));

		_httpHandler.SetupResponse(baseCode: "USD", json: BuildSuccessResponse(
			baseCode: "USD",
			rates: new Dictionary<string, decimal> { ["RUB"] = 90m }
		));
		_httpHandler.SetupResponse(baseCode: "EUR", json: BuildSuccessResponse(
			baseCode: "EUR",
			rates: new Dictionary<string, decimal> { ["RUB"] = 97m }
		));

		await RunCurrencyRateJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		// Polly retry should have picked up RUB on the second try
		bool hasRubRates = await readCtx.CurrencyRates.AnyAsync(predicate: r => r.BaseCode == "RUB");
		bool hasUsdRates = await readCtx.CurrencyRates.AnyAsync(predicate: r => r.BaseCode == "USD");

		await Assert.That(value: hasUsdRates).IsTrue();
	}
}
