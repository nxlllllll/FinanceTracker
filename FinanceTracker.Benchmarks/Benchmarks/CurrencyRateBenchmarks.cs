using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class CurrencyRateBenchmarks : BenchmarkBase
{
	private CurrencyRateReadRepository _repository = null!;

	private static readonly Currency Usd = Currency.Reconstitute(value: "USD");
	private static readonly Currency Eur = Currency.Reconstitute(value: "EUR");
	private static readonly Currency Rub = Currency.Reconstitute(value: "RUB");

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new CurrencyRateReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetRateAsync()
		=> await _repository.GetRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, date: DateOnly.FromDateTime(dateTime: DateTime.UtcNow));

	[Benchmark]
	public async Task GetLatestRateAsync()
		=> await _repository.GetLatestRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub);

	[Benchmark]
	public async Task GetRatesBatchAsync()
	{
		DateOnly today = DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
		IReadOnlyCollection<CurrencyRateRequest> requests =
		[
			new CurrencyRateRequest(From: Usd, To: Rub, Date: today),
			new CurrencyRateRequest(From: Eur, To: Rub, Date: today),
			new CurrencyRateRequest(From: Rub, To: Rub, Date: today),
		];

		await _repository.GetRatesBatchAsync(requests: requests);
	}

	[Benchmark]
	public async Task GetLatestRatesBatchAsync()
	{
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs =
		[
			new CurrencyLatestRateRequest(From: Usd, To: Rub),
			new CurrencyLatestRateRequest(From: Eur, To: Rub),
		];

		await _repository.GetLatestRatesBatchAsync(pairs: pairs);
	}
}