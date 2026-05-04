using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public class UserReadRepository(
	FinanceTrackerContext context
) : IUserReadRepository
{
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
}