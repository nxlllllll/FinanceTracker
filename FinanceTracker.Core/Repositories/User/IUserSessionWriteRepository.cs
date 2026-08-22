using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.Repositories.User;

public interface IUserSessionWriteRepository
{
	Task CreateAsync(
		UserSession session,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Guid>> RevokeAsync(
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Guid>> SupersedeAsync(
		Guid sessionId,
		Guid successorSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(
		Guid userId,
		Guid exceptSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Guid>> RevokeAllAsync(
		Guid userId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);
}
