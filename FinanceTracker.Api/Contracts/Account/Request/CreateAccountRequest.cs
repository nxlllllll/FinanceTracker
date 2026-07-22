using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Api.Contracts.Account.Request;

/// <summary>Body of <c>POST /api/v1/accounts</c>.</summary>
public sealed record CreateAccountRequest(
	string Name,
	AccountType Type,
	string Currency,
	decimal InitialBalance
);
