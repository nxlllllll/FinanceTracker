using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record AccountReadModel(
	Guid Id,
	Guid UserId,
	Name Name,
	AccountType Type,
	Money Balance,
	bool IsArchived,
	DateTimeOffset CreatedAt
) : IReadModel;
