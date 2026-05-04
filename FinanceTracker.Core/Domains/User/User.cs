using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.User;

public sealed class User
{
	public Guid Id { get; private set; }
	public Email Email { get; private set; }
	public string PasswordHash { get; private set; } = String.Empty;
	public Currency BaseCurrency { get; private set; }
	public DateTime CreatedAt { get; private set; }

	private User() { }

	public static User Register(
		DateTime createdAt,
		Email email,
		string passwordHash,
		Currency baseCurrency)
	{
		if (String.IsNullOrWhiteSpace(value: passwordHash))
			throw new PasswordException("The password hash cannot be empty.");
 
		return new User()
		{
			Id = Guid.NewGuid(),
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrency = baseCurrency,
			CreatedAt = createdAt
		};
	}

	public static User Reconstitute(
		Guid id,
		Email email,
		string passwordHash,
		Currency baseCurrencyCode,
		DateTime createdAt)
	{
		return new User()
		{
			Id = id,
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrency = baseCurrencyCode,
			CreatedAt = createdAt
		};
	}
	
	public void ChangeEmail(Email newEmail)
	{
		if (Email == newEmail)
			return;

		Email = newEmail;
	}
	
	public void ChangePassword(string newPasswordHash)
	{
		if (String.IsNullOrWhiteSpace(value: newPasswordHash))
			throw new PasswordException(message: "The password hash cannot be empty.");

		PasswordHash = newPasswordHash;
	}

	public void ChangeBaseCurrency(Currency newBaseCurrency)
	{
		if (BaseCurrency == newBaseCurrency)
			return;

		BaseCurrency = newBaseCurrency;
	}
}