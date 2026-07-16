using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record UserReadModel(
	Guid Id,
	Email Email,
	Currency BaseCurrency,
	DateTimeOffset CreatedAt
) : IReadModel;
