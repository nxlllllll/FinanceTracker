using System.Net;
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
	private const string BearerScheme = "Bearer";

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
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme: BearerScheme, parameter: currentOptions.ApiKey);

			HttpResponseMessage response = await httpClient.SendAsync(request: request, cancellationToken: ct);

			if (!response.IsSuccessStatusCode)
			{
				if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
				{
					logger.ZLogCritical(message: $"""
						[{nameof(ExchangeRateApiClient)}] Got {(int)response.StatusCode} {response.StatusCode} fetching rates for {baseCurrency}.
						This looks like a dead or missing API key, not a transient failure — it will not resolve on its own. Check ExchangeRateApiOptions.ApiKey.
					""");
				}
				else
				{
					logger.ZLogError(message: $"[{nameof(ExchangeRateApiClient)}] Got {(int)response.StatusCode} {response.StatusCode} fetching rates for {baseCurrency}.");
				}

				return null;
			}

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
