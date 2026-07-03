using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Notifications;

public sealed record BudgetDeactivatedNotification(
	Guid BudgetId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;
