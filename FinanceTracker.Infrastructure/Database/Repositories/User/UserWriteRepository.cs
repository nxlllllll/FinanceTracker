using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserWriteRepository(
	FinanceTrackerContext context
) : IUserWriteRepository
{
	private async Task ChangeUserPropertyAsync(
		Guid userId,
		Action<UpdateSettersBuilder<UserEntity>> changePropertyAction,
		CancellationToken ct = default)
	{
		await context.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(
			setPropertyCalls: changePropertyAction,
			cancellationToken: ct
		);
	}

	public async Task CreateAsync(
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		await context.Users.AddAsync(entity: new UserEntity()
		{
			Id = user.Id,
			Email = user.Email,
			PasswordHash = user.PasswordHash,
			BaseCurrencyCode = user.BaseCurrency,
			CreatedAt = user.CreatedAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task ChangeEmailAsync(
		Guid userId,
		Email newEmail,
		CancellationToken ct = default)
	{
		await ChangeUserPropertyAsync(
			userId: userId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: user => user.Email, valueExpression: newEmail),
			ct: ct
		);
	}

	public async Task ChangePasswordAsync(
		Guid userId,
		string newPasswordHash,
		CancellationToken ct = default)
	{
		await ChangeUserPropertyAsync(
			userId: userId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: user => user.PasswordHash, valueExpression: newPasswordHash),
			ct: ct
		);
	}

	public async Task ChangeBaseCurrencyAsync(
		Guid userId,
		Core.ValueObjects.Currency newBaseCurrencyCode,
		CancellationToken ct = default)
	{
		await ChangeUserPropertyAsync(
			userId: userId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: user => user.BaseCurrencyCode, valueExpression: newBaseCurrencyCode),
			ct: ct
		);
	}
}
