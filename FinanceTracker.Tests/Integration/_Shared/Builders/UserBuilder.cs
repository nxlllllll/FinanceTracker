using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

public class UserBuilder(FinanceTrackerContext context)
{
	private readonly CurrencyBuilder _currencyBuilder = new CurrencyBuilder(context: context);

	public async Task<Guid> CreateAsync(string currencyCode = "RUB")
	{
		await _currencyBuilder.CreateAsync(code: currencyCode);

		Guid userId = Guid.CreateVersion7();
		await context.Users.AddAsync(new UserEntity()
		{
			Id = userId,
			Email = Email.Create(value: $"{userId}@test.com").Value,
			PasswordHash = "hash",
			BaseCurrencyCode = Currency.Create(value: currencyCode).Value,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();
		return userId;
	}

	public async Task<Guid> CreateAsync(Currency currencyCode)
	{
		Guid userId = Guid.CreateVersion7();
		await context.Users.AddAsync(new UserEntity()
		{
			Id = userId,
			Email = Email.Create(value: $"{userId}@test.com").Value,
			PasswordHash = "hash",
			BaseCurrencyCode = currencyCode,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();
		return userId;
	}
}
