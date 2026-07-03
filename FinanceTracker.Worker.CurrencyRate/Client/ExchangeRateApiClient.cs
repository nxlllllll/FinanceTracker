using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate.Client;

/// <summary>
/// HTTP client for the ExchangeRate-API v6 service.
/// Wrapped with Polly retry and circuit breaker policies configured via <see cref="ExchangeRateApiOptions"/>.
/// Returns <c>null</c> when the client is disabled via configuration.
/// </summary>
public sealed class ExchangeRateApiClient(
	HttpClient httpClient,
	IOptionsMonitor<ExchangeRateApiOptions> options,
	ILogger<ExchangeRateApiClient> logger)
{
	private const string BearerShema = "Bearer";

	public async Task<ExchangeRateApiResponse?> GetRatesAsync(string baseCurrency, CancellationToken ct = default)
	{
		ExchangeRateApiOptions currentOptions = options.CurrentValue;

		if (!currentOptions.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{nameof(ExchangeRateApiClient)}] Disabled. Skipping fetch for {baseCurrency}.");
			return null;
		}

		string url = $"{currentOptions.BaseUrl}/latest/{baseCurrency}";

		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(method: HttpMethod.Get, requestUri: url);
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme: BearerShema, parameter: currentOptions.ApiKey);

			HttpResponseMessage response = await httpClient.SendAsync(request: request, cancellationToken: ct);

			response.EnsureSuccessStatusCode();

			ExchangeRateApiResponse? result = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse>(cancellationToken: ct);

			if (result?.Result == "success")
				return result;

			logger.ZLogWarning(message: $"ExchangeRateApi returned non-success result for {baseCurrency}: {result?.Result}.");
			return null;
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to fetch rates for {baseCurrency}.");
			return null;
		}
	}
}
