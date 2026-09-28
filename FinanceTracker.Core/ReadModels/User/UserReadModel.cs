using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.User;

public sealed record UserReadModel(
	Guid Id,
	Email Email,
	ValueObjects.Currency BaseCurrency,
	TimeZoneId TimeZone,
	NotificationType? NotificationType,
	DateTimeOffset CreatedAt
) : IReadModel;
