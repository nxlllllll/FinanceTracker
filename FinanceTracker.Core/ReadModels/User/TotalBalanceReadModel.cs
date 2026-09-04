using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.User;

/// <summary>
/// The sum of a user's accounts in their base currency, with <paramref name="IsApproximate"/> set
/// when at least one account was converted at a rate published before the requested date.
/// </summary>
public sealed record TotalBalanceReadModel(Money Total, bool IsApproximate);
