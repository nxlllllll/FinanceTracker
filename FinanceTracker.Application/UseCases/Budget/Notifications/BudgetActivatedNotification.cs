using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Notifications;

public sealed record BudgetActivatedNotification(
	Guid BudgetId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;