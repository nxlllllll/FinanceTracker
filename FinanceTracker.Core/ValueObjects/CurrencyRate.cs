using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Rate;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing an exchange rate between two currencies on a specific date.
/// Use <see cref="Create"/> for values arriving from outside the system, and
/// <see cref="Reconstitute"/> when loading from storage.
/// </summary>
public readonly record struct CurrencyRate
{
	/// <summary>The source currency being converted from.</summary>
	public Currency Base { get; }

	/// <summary>The target currency being converted to.</summary>
	public Currency Target { get; }

	/// <summary>Exchange rate: 1 unit of <see cref="Base"/> equals <see cref="Rate"/> units of <see cref="Target"/>.</summary>
	public decimal Rate { get; }

	/// <summary>The date for which this rate is valid.</summary>
	public DateOnly Date { get; }

	private CurrencyRate(Currency baseCurrency, Currency target, decimal rate, DateOnly date)
	{
		Base = baseCurrency;
		Target = target;
		Rate = rate;
		Date = date;
	}

	/// <summary>
	/// Creates a rate from an untrusted source, rounding it to <see cref="MonetaryScale.Rate"/>.
	/// Fails when the rate is not positive.
	/// </summary>
	public static Result<CurrencyRate, DomainException> Create(
		Currency baseCurrency,
		Currency target,
		decimal rate,
		DateOnly date)
	{
		decimal stored = Decimal.Round(d: rate, decimals: MonetaryScale.Rate, mode: MidpointRounding.ToEven);

		if (stored <= 0)
		{
			return Result<CurrencyRate, DomainException>.Failure(
				error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero at the stored precision.")
			);
		}

		return Result<CurrencyRate, DomainException>.Success(value: new CurrencyRate(
			baseCurrency: baseCurrency,
			target: target,
			rate: stored,
			date: date
		));
	}

	/// <summary>Bypasses validation. Use only when loading from a trusted storage source.</summary>
	public static CurrencyRate Reconstitute(Currency baseCurrency, Currency target, decimal rate, DateOnly date)
		=> new CurrencyRate(baseCurrency: baseCurrency, target: target, rate: rate, date: date);
}
