namespace FinanceTracker.Api.Contracts.Role.Request;

/// <summary>Body of <c>POST /api/v1/roles</c>. Permissions in "resource:action" form.</summary>
public sealed record CreateRoleRequest(
	string DisplayName,
	HashSet<string> Permissions
);
