using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.CurrencyRate.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class ExchangeRateApiClientTests
{
	private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
	{
		public HttpRequestMessage? LastRequest { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(result: response);
		}
	}

	private const string ApiKey = "super-secret-key";
	private const string BaseUrl = "https://v6.exchangerate-api.com/v6";

	private static (ExchangeRateApiClient Client, CapturingHttpMessageHandler Handler) BuildClient(bool isEnabled = true)
	{
		HttpResponseMessage response = new HttpResponseMessage(statusCode: HttpStatusCode.OK)
		{
			Content = JsonContent.Create(inputValue: new ExchangeRateApiResponse(
				Result: "success",
				BaseCode: "USD",
				ConversionRates: new Dictionary<string, decimal> { ["RUB"] = 90m }
			))
		};

		CapturingHttpMessageHandler handler = new CapturingHttpMessageHandler(response: response);
		HttpClient httpClient = new HttpClient(handler: handler);

		ExchangeRateApiOptions options = new ExchangeRateApiOptions
		{
			IsEnabled = isEnabled,
			ApiKey = ApiKey,
			BaseUrl = BaseUrl
		};

		ExchangeRateApiClient client = new ExchangeRateApiClient(
			httpClient: httpClient,
			options: new FakeOptionsMonitor<ExchangeRateApiOptions>(value: options),
			logger: NullLogger<ExchangeRateApiClient>.Instance
		);

		return (client, handler);
	}

	[Test]
	public async Task GetRatesAsync_ShouldNotIncludeApiKeyInRequestUrl()
	{
		(ExchangeRateApiClient client, CapturingHttpMessageHandler handler) = BuildClient();

		await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: handler.LastRequest).IsNotNull();
		await Assert.That(value: handler.LastRequest!.RequestUri!.ToString().Contains(value: ApiKey)).IsFalse();
	}

	[Test]
	public async Task GetRatesAsync_ShouldSendApiKeyAsBearerAuthorizationHeader()
	{
		(ExchangeRateApiClient client, CapturingHttpMessageHandler handler) = BuildClient();

		await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: handler.LastRequest!.Headers.Authorization).IsNotNull();
		await Assert.That(value: handler.LastRequest!.Headers.Authorization!.Scheme).IsEqualTo(expected: "Bearer");
		await Assert.That(value: handler.LastRequest!.Headers.Authorization!.Parameter).IsEqualTo(expected: ApiKey);
	}

	[Test]
	public async Task GetRatesAsync_ShouldRequestExactExpectedUrl()
	{
		(ExchangeRateApiClient client, CapturingHttpMessageHandler handler) = BuildClient();

		await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: handler.LastRequest!.RequestUri!.ToString())
			.IsEqualTo(expected: "https://v6.exchangerate-api.com/v6/latest/USD");
	}

	[Test]
	public async Task GetRatesAsync_WhenDisabled_ShouldNotSendRequest()
	{
		(ExchangeRateApiClient client, CapturingHttpMessageHandler handler) = BuildClient(isEnabled: false);

		ExchangeRateApiResponse? result = await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
		await Assert.That(value: handler.LastRequest).IsNull();
	}

	[Test]
	public async Task GetRatesAsync_WhenSuccessful_ShouldReturnResponse()
	{
		(ExchangeRateApiClient client, _) = BuildClient();

		ExchangeRateApiResponse? result = await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Result).IsEqualTo(expected: "success");
		await Assert.That(value: result.ConversionRates[key: "RUB"]).IsEqualTo(expected: 90m);
	}

	[Test]
	public async Task GetRatesAsync_WhenHttpCallFails_ShouldReturnNull()
	{
		CapturingHttpMessageHandler handler = new CapturingHttpMessageHandler(
			response: new HttpResponseMessage(statusCode: HttpStatusCode.ServiceUnavailable)
		);
		HttpClient httpClient = new HttpClient(handler: handler);

		ExchangeRateApiOptions options = new ExchangeRateApiOptions
		{
			IsEnabled = true,
			ApiKey = ApiKey,
			BaseUrl = BaseUrl
		};

		ExchangeRateApiClient client = new ExchangeRateApiClient(
			httpClient: httpClient,
			options: new FakeOptionsMonitor<ExchangeRateApiOptions>(value: options),
			logger: NullLogger<ExchangeRateApiClient>.Instance
		);

		ExchangeRateApiResponse? result = await client.GetRatesAsync(baseCurrency: "USD", ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
	}
}