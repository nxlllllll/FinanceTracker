using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RegisterUserCommandFactory
{
	public static RegisterUserCommand CreateWithIp(IPAddress? ipAddress)
	{
		return new RegisterUserCommand(
			Email: Email.Create(value: "test@test.com").Value,
			Password: "password123",
			BaseCurrencyCode: Currency.Reconstitute(value: "RUB"),
#pragma warning disable CS8604 // Possible null reference argument.
			IpAddress: ipAddress
#pragma warning restore CS8604 // Possible null reference argument.
		);
	}

	public static RegisterUserCommand Create(
		string email = "test@test.com",
		string password = "password123",
		string baseCurrencyCode = "RUB",
		IPAddress? ipAddress = null)
	{
		return new RegisterUserCommand(
			Email: Email.Create(value: email).Value,
			Password: password,
			BaseCurrencyCode: Currency.Create(value: baseCurrencyCode).Value,
			IpAddress: ipAddress ?? IPAddress.Parse(ipString: "203.0.113.10")
		);
	}
}
