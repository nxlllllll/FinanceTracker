using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Repositories.Abstractions;

namespace FinanceTracker.Core.Repositories.UserPermission;

public interface IUserPermissionWriteRepository
{
	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task GrantAsync(
		PermissionGranted @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task RevokeAsync(
		PermissionRevoked @event,
		CancellationToken ct = default
	);

	Task<int> DeleteOldTombstonesAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);
}
