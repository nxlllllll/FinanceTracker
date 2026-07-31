namespace FinanceTracker.Api.Endpoints.Users.Contracts;

/// <summary>Body of <c>POST /api/v1/users/{userId}/permissions</c>.</summary>
public sealed record GrantPermissionRequest(string Permission);
