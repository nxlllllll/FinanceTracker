using FinanceTracker.Application.Users.Commands.RegisterUser;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RegisterUserCommandFactory
{
	public static RegisterUserCommand Create(
		string email = "test@test.com",
		string passwordHash = "hash",
		string baseCurrencyCode = "RUB")
	{
		return new RegisterUserCommand(
			Email: email,
			PasswordHash: passwordHash,
			BaseCurrencyCode: baseCurrencyCode
		);
	}
}