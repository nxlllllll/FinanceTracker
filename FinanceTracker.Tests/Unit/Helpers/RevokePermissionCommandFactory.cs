using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RevokePermissionCommandFactory
{
	public static RevokePermissionCommand Create(
		Guid? targetUserId = null,
		Resource resource = Resource.Account,
		PermissionAction action = PermissionAction.Write,
		Guid? revokedBy = null
	) => new RevokePermissionCommand(
		TargetUserId: targetUserId ?? Guid.CreateVersion7(),
		Permission: Permission.Create(resource: resource, action: action).Value!,
		RevokedBy: revokedBy ?? Guid.CreateVersion7()
	);
}
