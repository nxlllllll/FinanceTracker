using FinanceTracker.Core.Domains.UserPermission;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class UserPermissionFactory
{
	public static UserPermission Create(Guid? userId = null) => UserPermission.Create(
		occurredAt: FakeDateProvider.Default.UtcNow,
		userId: userId ?? Guid.CreateVersion7()
	).Value!;

	public static UserPermission CreateWithGrant(
		Guid? userId = null,
		Guid? grantedBy = null,
		Resource resource = Resource.Account,
		PermissionAction action = PermissionAction.Read)
	{
		UserPermission userPermission = Create(userId: userId);

		Permission permission = Permission.Create(resource: resource, action: action).Value!;
		userPermission.Grant(
			occurredAt: FakeDateProvider.Default.UtcNow,
			grantedBy: grantedBy ?? Guid.CreateVersion7(),
			permission: permission
		);
		userPermission.ClearEvents();

		return userPermission;
	}
}
