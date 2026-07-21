using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.Role;

public sealed record RoleDto(
	Guid Id,
	SystemRole? SystemKey,
	Name DisplayName,
	IReadOnlySet<Permission> Permissions
);
