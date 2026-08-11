using FinanceTracker.Core.Domains.UserRole;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class UserRoleFactory
{
	public static UserRole Create(Guid? userId = null) => UserRole.Create(
		occurredAt: FakeDateProvider.Default.UtcNow,
		userId: userId ?? Guid.CreateVersion7()
	).Value!;

	public static UserRole CreateWithRole(
		Guid? userId = null,
		Guid? roleId = null,
		Guid? assignedBy = null)
	{
		UserRole userRole = Create(userId: userId);

		userRole.Assign(
			occurredAt: FakeDateProvider.Default.UtcNow,
			roleId: roleId ?? Guid.CreateVersion7(),
			assignedBy: assignedBy ?? Guid.CreateVersion7()
		);
		userRole.ClearEvents();

		return userRole;
	}
}
