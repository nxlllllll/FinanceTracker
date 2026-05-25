namespace FinanceTracker.Core.Services.Token;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
