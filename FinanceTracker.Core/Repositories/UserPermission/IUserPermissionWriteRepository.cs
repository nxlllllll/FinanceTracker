using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Repositories.Abstractions;

namespace FinanceTracker.Core.Repositories.UserPermission;

public interface IUserPermissionWriteRepository
{
	[EventuallyConsistentCreate]
	Task GrantAsync(
		PermissionGranted @event,
		CancellationToken ct = default
	);

	Task RevokeAsync(
		Guid userId,
		string permission,
		CancellationToken ct = default
	);
}
