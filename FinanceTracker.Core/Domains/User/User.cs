using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
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

	public static Result<User, DomainException> Register(
		DateTime createdAt,
		Email email,
		string passwordHash,
		Currency baseCurrency)
	{
		if (String.IsNullOrWhiteSpace(value: passwordHash))
			return Result<User, DomainException>.Failure(error: new PasswordException("The password hash cannot be empty."));
 
		return Result<User, DomainException>.Success(value: new User()
		{
			Id = Guid.NewGuid(),
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrency = baseCurrency,
			CreatedAt = createdAt
		});
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
	
	public Result<Unit, DomainException> ChangeEmail(Email newEmail)
	{
		if (Email == newEmail)
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		Email = newEmail;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> ChangePassword(string newPasswordHash)
	{
		if (String.IsNullOrWhiteSpace(value: newPasswordHash))
			return Result<Unit, DomainException>.Failure(error: new PasswordException(message: "The password hash cannot be empty."));
 
		PasswordHash = newPasswordHash;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> ChangeBaseCurrency(Currency newBaseCurrency)
	{
		if (BaseCurrency == newBaseCurrency)
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		BaseCurrency = newBaseCurrency;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}