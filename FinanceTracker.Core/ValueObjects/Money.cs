using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

[JsonConverter(converterType: typeof(MoneyJsonConverter))]
public readonly record struct Money
{
	public decimal Amount { get; }
	public Currency Currency { get; }

	private Money(decimal amount, Currency currency)
	{
		Amount = amount;
		Currency = currency;
	}

	public static Result<Money, DomainException> Create(decimal amount, Currency currency)
	{
		if (amount < 0)
			return Result<Money, DomainException>.Failure(error: new InvalidAmountException(message: "Amount cannot be negative."));

		return Result<Money, DomainException>.Success(value: new Money(amount: amount, currency: currency));
	}

	public static Result<Money, DomainException> Positive(decimal amount, Currency currency)
	{
		if (amount <= 0)
			return Result<Money, DomainException>.Failure(error: new InvalidAmountException(message: "Amount must be greater than zero."));

		return Result<Money, DomainException>.Success(value: new Money(amount: amount, currency: currency));
	}
	
	public static Money Reconstitute(decimal amount, Currency currency)
		=> new Money(amount: amount, currency: currency);

	public static Money operator +(Money left, decimal right)
		=> new Money(amount: left.Amount + right, currency: left.Currency);

	public static Money operator -(Money left, decimal right)
		=> new Money(amount: left.Amount - right, currency: left.Currency);

	public static Money operator *(Money left, decimal right)
		=> new Money(amount: left.Amount * right, currency: left.Currency);

	public override string ToString()
		=> $"{Amount.ToString(format: null, provider: System.Globalization.CultureInfo.InvariantCulture)} {Currency}";
}