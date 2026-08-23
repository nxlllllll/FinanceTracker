using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.User;

public sealed record UserReadModel(
	Guid Id,
	Email Email,
	ValueObjects.Currency BaseCurrency,
	TimeZoneId TimeZone,
	DateTimeOffset CreatedAt
) : IReadModel;
