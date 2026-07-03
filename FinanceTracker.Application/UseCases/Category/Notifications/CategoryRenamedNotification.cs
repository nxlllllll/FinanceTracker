using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Notifications;

public sealed record CategoryRenamedNotification(
	Guid CategoryId,
	Guid UserId,
	string OldName,
	string NewName,
	DateTimeOffset OccurredAt
) : INotification;
