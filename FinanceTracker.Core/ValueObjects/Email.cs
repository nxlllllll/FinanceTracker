using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Email
{
	public string Value { get; }
 
	public Email(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			throw new EmailException(message: "The email cannot be empty.", email: value);
 
		Value = value.Trim().ToLowerInvariant();
	}
 
	public static implicit operator string(Email email) 
		=> email.Value;
	
	public static implicit operator Email(string value)
		=> new Email(value);
 
	public override string ToString()
		=> Value;
}