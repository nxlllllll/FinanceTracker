using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class UserFactory
{
	public static Result<User, DomainException> Create(
		string email = "test@test.com",
		string passwordHash = "hash",
		string baseCurrencyCode = "RUB")
	{
		Result<Email, DomainException> emailResult = Email.Create(value: email);
		if (emailResult.IsFailure)
			return Result<User, DomainException>.Failure(error: emailResult.Error!);

		Result<Currency, DomainException> currencyResult = Currency.Create(value: baseCurrencyCode);
		if (currencyResult.IsFailure)
			return Result<User, DomainException>.Failure(error: currencyResult.Error!);
		
		Result<User, DomainException> result = User.Register(
			createdAt: FakeDateProvider.Default.UtcNow,
			email: email,
			passwordHash: passwordHash,
			baseCurrency: baseCurrencyCode
		);

		return result;
	}
}