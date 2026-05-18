using FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RegisterUserCommandFactory
{
	public static RegisterUserCommand Create(
		string email = "test@test.com",
		string password = "password123",
		string baseCurrencyCode = "RUB")
	{
		return new RegisterUserCommand(
			Email: email,
			Password: password,
			BaseCurrencyCode: baseCurrencyCode
		);
	}
}