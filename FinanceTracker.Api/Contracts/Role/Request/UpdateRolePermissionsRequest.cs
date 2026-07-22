namespace FinanceTracker.Api.Contracts.Role.Request;

/// <summary>Body of <c>PATCH /api/v1/roles/{roleId}/permissions</c>. Replaces the entire set.</summary>
public sealed record UpdateRolePermissionsRequest(HashSet<string> Permissions);
