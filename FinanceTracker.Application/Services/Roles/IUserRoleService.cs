using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.Services.Roles;

/// <summary>
/// Assigns and removes roles, applying the permissions they carry.
/// </summary>
public interface IUserRoleService
{
	/// <summary>
	/// Assigns <paramref name="roleId"/> to <paramref name="userId"/> and grants the permissions
	/// </summary>
	Task<Result<Unit, AppException>> AssignAsync(
		Guid userId,
		Guid roleId,
		Guid assignedBy,
		CancellationToken ct = default
	);

	/// <summary>
	/// Removes <paramref name="roleId"/> from <paramref name="userId"/> and revokes the permissions
	/// </summary>
	Task<Result<Unit, AppException>> RemoveAsync(
		Guid userId,
		Guid roleId,
		Guid removedBy,
		CancellationToken ct = default
	);
}
