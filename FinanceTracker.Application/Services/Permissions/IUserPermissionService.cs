using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.Services.Permissions;

/// <summary>
/// Applies permission changes to a user's permission aggregate.
/// </summary>
public interface IUserPermissionService
{
	/// <summary>
	/// Grants <paramref name="permissions"/> to <paramref name="targetUserId"/> in a single load and save.
	/// </summary>
	Task<Result<Unit, AppException>> GrantAsync(
		Guid targetUserId,
		Guid grantedBy,
		IReadOnlyCollection<Permission> permissions,
		CancellationToken ct = default
	);

	/// <summary>
	/// Revokes <paramref name="permissions"/> from <paramref name="targetUserId"/> in a single load and save.
	/// </summary>
	Task<Result<Unit, AppException>> RevokeAsync(
		Guid targetUserId,
		Guid revokedBy,
		IReadOnlyCollection<Permission> permissions,
		CancellationToken ct = default
	);
}
