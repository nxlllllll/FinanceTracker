using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserRepository(
	FinanceTrackerContext context
) : IUserRepository
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
	
	public async Task<Core.Domains.User.User?> GetByIdAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking()
			.Where(predicate: user => user.Id == userId)
			.Select(selector: user => Core.Domains.User.User.Reconstitute(
				id: user.Id,
				email: user.Email,
				passwordHash: user.PasswordHash,
				baseCurrencyCode: user.BaseCurrencyCode,
				createdAt: user.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<Core.Domains.User.User?> GetByEmailAsync(
		string email,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking()
			.Where(predicate: user => user.Email == email)
			.Select(selector: user => Core.Domains.User.User.Reconstitute(
				id: user.Id,
				email: user.Email,
				passwordHash: user.PasswordHash,
				baseCurrencyCode: user.BaseCurrencyCode,
				createdAt: user.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);

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
			BaseCurrencyCode = user.BaseCurrencyCode,
			CreatedAt = user.CreatedAt
		}, cancellationToken: ct);
		
		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task ChangeEmailAsync(
		Guid userId,
		string newEmail,
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
		string newBaseCurrencyCode,
		CancellationToken ct = default)
	{
		await ChangeUserPropertyAsync(
			userId: userId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: user => user.BaseCurrencyCode, valueExpression: newBaseCurrencyCode),
			ct: ct
		);
	}
}