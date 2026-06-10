using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Notifications;

public sealed record BudgetPeriodChangedNotification(
	Guid BudgetId,
	Guid UserId,
	DateOnly NewFrom,
	DateOnly NewTo,
	DateTimeOffset OccurredAt
) : INotification;