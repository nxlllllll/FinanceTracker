using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Npgsql;

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

		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
		}
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			throw new EmailException(message: "The user with this email address already exists.", email: user.Email.Value);
		}
	}

	public async Task ChangeEmailAsync(
		Guid userId,
		Email newEmail,
		CancellationToken ct = default)
	{
		try
		{
			await ChangeUserPropertyAsync(
				userId: userId,
				changePropertyAction: builder => builder.SetProperty(propertyExpression: user => user.Email, valueExpression: newEmail),
				ct: ct
			);
		}
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			throw new EmailException(message: "The user with this email address already exists.", email: newEmail.Value);
		}
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
