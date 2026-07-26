namespace FinanceTracker.Api.Endpoints.Roles.Contracts;

/// <summary>Body of <c>PATCH /api/v1/roles/{roleId}/permissions</c>. Replaces the entire set.</summary>
public sealed record UpdateRolePermissionsRequest(HashSet<string> Permissions);
