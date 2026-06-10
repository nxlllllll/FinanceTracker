using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionCreatedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	int DayOfMonth,
	string? Description,
	DateTimeOffset OccurredAt
) : INotification;