using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class UserBuilder(FinanceTrackerContext context )
{
	private readonly CurrencyBuilder _currencyBuilder = new CurrencyBuilder(context: context);
	
	public async Task<Guid> CreateAsync(
		string currencyCode = "RUB")
	{
		await _currencyBuilder.CreateAsync(code: currencyCode);

		Guid userId = Guid.NewGuid();
		await context.Users.AddAsync(new UserEntity()
		{
			Id = userId,
			Email = $"{userId}@test.com",
			PasswordHash = "hash",
			BaseCurrencyCode = currencyCode,
			CreatedAt = DateTime.UtcNow
		});
		await context.SaveChangesAsync();
		return userId;
	}
}