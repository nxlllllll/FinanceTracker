using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate.Client;

public sealed class ExchangeRateApiClient(
	HttpClient httpClient,
	IOptionsMonitor<ExchangeRateApiOptions> options,
	ILogger<ExchangeRateApiClient> logger)
{
	private readonly ExchangeRateApiOptions _options = options.CurrentValue;

	public async Task<ExchangeRateApiResponse?> GetRatesAsync(string baseCurrency, CancellationToken ct = default)
	{
		if (!_options.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{nameof(ExchangeRateApiClient)}] Disabled. Skipping fetch for {baseCurrency}.");
			return null;
		}

		string url = $"{_options.BaseUrl}/{_options.ApiKey}/latest/{baseCurrency}";

		try
		{
			HttpResponseMessage response = await httpClient.GetAsync(requestUri: url, cancellationToken: ct);

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