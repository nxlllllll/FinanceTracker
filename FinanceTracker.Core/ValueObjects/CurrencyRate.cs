using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct CurrencyRate
{
	public Currency Base { get; }
	public Currency Target { get; }
	public decimal Rate { get; }
	public DateOnly Date { get; }

	private CurrencyRate(Currency baseCurrency, Currency target, decimal rate, DateOnly date)
	{
		Base = baseCurrency;
		Target = target;
		Rate = rate;
		Date = date;
	}

	public static Result<CurrencyRate, DomainException> Create(
		Currency baseCurrency,
		Currency target,
		decimal rate,
		DateOnly date)
	{
		if (rate <= 0)
			return Result<CurrencyRate, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		return Result<CurrencyRate, DomainException>.Success(value: new CurrencyRate(baseCurrency: baseCurrency, target: target, rate: rate, date: date));
	}

	public static CurrencyRate Reconstitute(Currency baseCurrency, Currency target, decimal rate, DateOnly date)
		=> new CurrencyRate(baseCurrency: baseCurrency, target: target, rate: rate, date: date);
}