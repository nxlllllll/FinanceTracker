using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.RecurringTransaction;

public sealed record RecurringTransactionReadModel(
	Guid Id,
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	int DayOfMonth,
	DateTimeOffset NextDueAtUtc,
	TimeZoneId TimeZone,
	string? Description,
	bool IsActive,
	int RowVersion,
	DateTimeOffset? LastExecutedAt,
	DateTimeOffset? LastMissedAt,
	DateTimeOffset CreatedAt
) : IReadModel;
