using System.Text.Json.Serialization;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Money
{
	public decimal Amount { get; }
	public Currency Currency { get; }
	
	[JsonConstructor]
	public Money(decimal amount, Currency currency)
	{
		if (amount < 0)
			throw new InvalidAmountException(message: "Amount cannot be negative.");

		Amount = amount;
		Currency = currency;
	}
 
	internal Money(decimal amount, Currency currency, bool allowNegative = true)
	{
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
		=> new Money(amount: left.Amount + right, currency: left.Currency);
	
	public static Money operator -(Money left, decimal right)
		=> new Money(amount: left.Amount - right, currency: left.Currency);
	
	public static Money operator *(Money left, decimal right)
		=> new Money(amount: left.Amount * right, currency: left.Currency);
 
	public override string ToString() 
		=> $"{Amount} {Currency}";
}