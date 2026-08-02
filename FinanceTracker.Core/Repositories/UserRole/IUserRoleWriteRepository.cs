using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Repositories.Abstractions;

namespace FinanceTracker.Core.Repositories.UserRole;

public interface IUserRoleWriteRepository
{
	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task AssignAsync(
		RoleAssigned @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task RemoveAsync(
		RoleRemoved @event,
		CancellationToken ct = default
	);
}
