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
			createdAt: FakeDateProvider.Default.UtcNow,
			email: email,
			passwordHash: passwordHash,
			baseCurrency: baseCurrencyCode
		);
	}
}