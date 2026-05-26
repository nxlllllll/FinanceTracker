using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RegisterUserCommandFactory
{
	public static RegisterUserCommand Create(
		string email = "test@test.com",
		string password = "password123",
		string baseCurrencyCode = "RUB")
	{
		return new RegisterUserCommand(
			Email: Email.Create(value: email).Value,
			Password: password,
			BaseCurrencyCode: Currency.Create(value: baseCurrencyCode).Value
		);
	}
}
