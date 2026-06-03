using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserSessionWriteRepository(
	FinanceTrackerContext context
) : IUserSessionWriteRepository
{
	public async Task CreateAsync(
		Core.Domains.User.UserSession session,
		CancellationToken ct = default)
	{
		await context.UserSessions.AddAsync(entity: new UserSessionEntity
		{
			Id = session.Id,
			UserId = session.UserId,
			RefreshTokenHash = session.RefreshTokenHash,
			ExpiresAt = session.ExpiresAt,
			CreatedAt = session.CreatedAt,
			RevokedAt = session.RevokedAt
		}, cancellationToken: ct);
	}

	public async Task RevokeAsync(
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		await context.UserSessions.Where(predicate: s => s.Id == sessionId).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(propertyExpression: x => x.RevokedAt, valueExpression: revokedAt),
			cancellationToken: ct
		);
	}
}