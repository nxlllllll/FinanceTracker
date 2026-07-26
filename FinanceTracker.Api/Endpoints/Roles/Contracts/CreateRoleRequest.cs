namespace FinanceTracker.Api.Endpoints.Roles.Contracts;

/// <summary>Body of <c>POST /api/v1/roles</c>. Permissions in "resource:action" form.</summary>
public sealed record CreateRoleRequest(
	string DisplayName,
	HashSet<string> Permissions
);
