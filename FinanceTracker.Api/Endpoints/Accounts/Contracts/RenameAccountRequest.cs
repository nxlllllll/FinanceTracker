namespace FinanceTracker.Api.Endpoints.Accounts.Contracts;

/// <summary>Body of <c>POST /api/v1/accounts/{id}/rename</c>.</summary>
public sealed record RenameAccountRequest(string NewName);
