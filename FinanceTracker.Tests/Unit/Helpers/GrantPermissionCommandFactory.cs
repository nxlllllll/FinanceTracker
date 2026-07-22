using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class GrantPermissionCommandFactory
{
	public static GrantPermissionCommand Create(
		Guid? targetUserId = null,
		Resource resource = Resource.Account,
		PermissionAction action = PermissionAction.Write,
		Guid? grantedBy = null
	) => new GrantPermissionCommand(
		TargetUserId: targetUserId ?? Guid.CreateVersion7(),
		Permission: Permission.Create(resource: resource, action: action).Value!,
		GrantedBy: grantedBy ?? Guid.CreateVersion7()
	);
}
