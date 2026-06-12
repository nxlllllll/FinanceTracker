using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserWriteRepository(
	FinanceTrackerContext context
) : IUserWriteRepository
{
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
			RowVersion = 0,
			CreatedAt = user.CreatedAt
		}, cancellationToken: ct);
	}

	public async Task ChangeEmailAsync(
		Guid userId,
		Email newEmail,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Users.Where(predicate: u => u.Id == userId && u.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: u => u.Email, valueExpression: newEmail)
				.SetProperty(propertyExpression: u => u.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"User {userId} was modified by another request.", id: userId);
	}

	public async Task ChangePasswordAsync(
		Guid userId,
		string newPasswordHash,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Users.Where(predicate: u => u.Id == userId && u.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: u => u.PasswordHash, valueExpression: newPasswordHash)
				.SetProperty(propertyExpression: u => u.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"User {userId} was modified by another request.", id: userId);
	}

	public async Task ChangeBaseCurrencyAsync(
		Guid userId,
		Core.ValueObjects.Currency newBaseCurrencyCode,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Users.Where(predicate: u => u.Id == userId && u.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: u => u.BaseCurrencyCode, valueExpression: newBaseCurrencyCode)
				.SetProperty(propertyExpression: u => u.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"User {userId} was modified by another request.", id: userId);
	}
}