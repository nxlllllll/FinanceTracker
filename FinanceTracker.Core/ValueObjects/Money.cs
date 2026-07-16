using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in a specific currency.
/// Use <see cref="Create"/> for user-supplied values (validates non-negative amount),
/// or <see cref="Positive"/> when a strictly positive amount is required.
/// Use <see cref="Reconstitute"/> when rehydrating from storage without re-validation.
/// </summary>
[JsonConverter(converterType: typeof(MoneyJsonConverter))]
public readonly record struct Money
{
	/// <summary>
	/// The monetary amount. Non-negative for any value built through <see cref="Create"/> or
	/// <see cref="Positive"/>. May be negative on values produced by internal event-sourced
	/// corrections (see the type-level remarks) — callers that depend on non-negativity should
	/// not assume it holds unconditionally.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>The currency of this amount.</summary>
	public Currency Currency { get; }

	private Money(decimal amount, Currency currency)
	{
		Amount = amount;
		Currency = currency;
	}

	/// <summary>
	/// Creates a <see cref="Money"/> value. Fails if <paramref name="amount"/> is negative.
	/// </summary>
	public static Result<Money, DomainException> Create(decimal amount, Currency currency)
	{
		if (amount < 0)
			return Result<Money, DomainException>.Failure(error: new InvalidAmountException(message: "Amount cannot be negative."));

		return Result<Money, DomainException>.Success(value: new Money(amount: amount, currency: currency));
	}

	/// <summary>
	/// Creates a <see cref="Money"/> value. Fails if <paramref name="amount"/> is zero or negative.
	/// Use when a debit or credit must have a positive value.
	/// </summary>
	public static Result<Money, DomainException> Positive(decimal amount, Currency currency)
	{
		if (amount <= 0)
			return Result<Money, DomainException>.Failure(error: new InvalidAmountException(message: "Amount must be greater than zero."));

		return Result<Money, DomainException>.Success(value: new Money(amount: amount, currency: currency));
	}

	/// <summary>
	/// Bypasses validation. Use only when rehydrating from a trusted storage source.
	/// </summary>
	public static Money Reconstitute(decimal amount, Currency currency)
		=> new Money(amount: amount, currency: currency);

	/// <summary>Converts <paramref name="amount"/> by <paramref name="rate"/> and rounds the result to 2 decimal places</summary>
	/// <remarks>
	/// Use this everywhere a converted amount is computed, instead of multiplying directly.
	/// The event-sourced aggregate is rebuilt by replaying every event from scratch, while
	/// projections (read models) apply the same calculation incrementally as deltas. If the
	/// multiplication isn't rounded identically in both places, the two sides can silently
	/// drift apart — a fraction of a cent at a time — across many FX-converted operations on
	/// the same account. Centralising the calculation here keeps them consistent by construction.
	/// </remarks>
	public static decimal ConvertedAmount(decimal amount, decimal rate)
		=> Math.Round(d: amount * rate, decimals: 2, mode: MidpointRounding.ToEven);

	/// <summary>
	/// Adds a raw decimal to this amount without currency validation and without
	/// re-checking non-negativity — the result may legitimately be negative
	/// Internal use only — called from event sourcing Apply methods where currency is guaranteed.
	/// </summary>
	internal Money Add(decimal amount)
		=> new Money(amount: Amount + amount, currency: Currency);

	/// <summary>
	/// Subtracts a raw decimal from this amount without currency validation and without
	/// re-checking non-negativity — the result may legitimately be negative
	/// Internal use only — called from event sourcing Apply methods where currency is guaranteed.
	/// </summary>
	internal Money Subtract(decimal amount)
		=> new Money(amount: Amount - amount, currency: Currency);

	/// <summary>Adds two monetary amounts. Throws if currencies differ.</summary>
	public Money Add(Money value)
	{
		if (Currency != value.Currency)
			throw new CurrencyException(message: $"Cannot add amounts of different currencies: {Currency} and {value.Currency}.");

		return Add(amount: value.Amount);
	}

	/// <summary>Subtracts two monetary amounts. Throws if currencies differ.</summary>
	public Money Subtract(Money value)
	{
		if (Currency != value.Currency)
			throw new CurrencyException(message: $"Cannot subtract amounts of different currencies: {Currency} and {value.Currency}.");

		return Subtract(amount: value.Amount);
	}

	/// <inheritdoc/>
	/// <returns>Returns a string representation of the currency, for example, <c>RUB</c></returns>
	public override string ToString()
		=> $"{Amount.ToString(format: null, provider: System.Globalization.CultureInfo.InvariantCulture)} {Currency}";
}
