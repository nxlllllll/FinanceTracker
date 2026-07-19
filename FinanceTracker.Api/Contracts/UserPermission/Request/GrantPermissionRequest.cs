namespace FinanceTracker.Api.Contracts.UserPermission.Request;

/// <summary>Body of <c>POST /api/v1/users/{userId}/permissions</c>.</summary>
public sealed record GrantPermissionRequest(string Permission );
