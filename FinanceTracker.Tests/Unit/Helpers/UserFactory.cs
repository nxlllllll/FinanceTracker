using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class UserFactory
{
	public static User Create(
		string email = "test@test.com",
		string passwordHash = "hash",
		string baseCurrencyCode = "RUB")
	{
		return User.Register(
			email: email,
			passwordHash: passwordHash,
			baseCurrencyCode: baseCurrencyCode
		);
	}
}