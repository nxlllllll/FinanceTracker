using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.User;

public sealed class User
{
	public Guid Id { get; private set; }
	public string Email { get; private set; } = String.Empty;
	public string PasswordHash { get; private set; } = String.Empty;
	public string BaseCurrencyCode { get; private set; } = String.Empty;
	public DateTime CreatedAt { get; private set; }

	private User() { }

	public static User Register(
		string email,
		string passwordHash,
		string baseCurrencyCode)
	{
		if (String.IsNullOrWhiteSpace(value: email))
			throw new EmailException(message: "The email cannot be empty.", email: email);

		if (String.IsNullOrWhiteSpace(value: passwordHash))
			throw new PasswordException(message: "The password hash cannot be empty.");

		if (String.IsNullOrWhiteSpace(value: baseCurrencyCode))
			throw new CurrencyException(message: "The base currency code cannot be empty.");

		return new User()
		{
			Id = Guid.NewGuid(),
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrencyCode = baseCurrencyCode,
			CreatedAt = DateTime.UtcNow
		};
	}

	public static User Reconstitute(
		Guid id,
		string email,
		string passwordHash,
		string baseCurrencyCode,
		DateTime createdAt)
	{
		return new User()
		{
			Id = id,
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrencyCode = baseCurrencyCode,
			CreatedAt = createdAt
		};
	}
	
	public void ChangeEmail(string newEmail)
	{
		if (String.IsNullOrWhiteSpace(value: newEmail))
			throw new EmailException(message: "The email cannot be empty.", email: newEmail);

		if (Email.Equals(value: newEmail))
			return;

		Email = newEmail;
	}
	
	public void ChangePassword(string newPasswordHash)
	{
		if (String.IsNullOrWhiteSpace(value: newPasswordHash))
			throw new PasswordException(message: "The password hash cannot be empty.");

		PasswordHash = newPasswordHash;
	}

	public void ChangeBaseCurrency(string newBaseCurrencyCode)
	{
		if (String.IsNullOrWhiteSpace(newBaseCurrencyCode))
			throw new CurrencyException(message: "The base currency code cannot be empty.");

		if (BaseCurrencyCode.Equals(value: newBaseCurrencyCode))
			return;

		BaseCurrencyCode = newBaseCurrencyCode;
	}
}