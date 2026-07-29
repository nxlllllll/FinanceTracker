namespace FinanceTracker.Api.Endpoints.UserPermissions.Contracts;

/// <summary>Body of <c>POST /api/v1/users/{userId}/permissions</c>.</summary>
public sealed record GrantPermissionRequest(string Permission);
