using System.Text.Json.Serialization;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Money
{
	public decimal Amount { get; }
	public Currency Currency { get; }
 
	private Money(decimal amount, Currency currency, bool _)
	{
		Amount = amount;
		Currency = currency;
	}
	
	[JsonConstructor]
	public Money(decimal amount, Currency currency)
	{
		if (amount < 0)
			throw new InvalidAmountException(message: "Amount cannot be negative.");

		Amount = amount;
		Currency = currency;
	}
 
	public static Money Positive(decimal amount, Currency currency)
	{
		if (amount <= 0)
			throw new InvalidAmountException(message: "Amount must be greater than zero.");
 
		return new Money(amount, currency);
	}
	
	public static Money operator +(Money left, decimal right)
		=> new Money(amount: left.Amount + right, currency: left.Currency, _: false);
	
	public static Money operator -(Money left, decimal right)
		=> new Money(amount: left.Amount - right, currency: left.Currency, _: false);
	
	public static Money operator *(Money left, decimal right)
		=> new Money(amount: left.Amount * right, currency: left.Currency, _: false);
 
	public override string ToString() 
		=> $"{Amount} {Currency}";
}