using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserNotificationType;

public sealed record ChangeUserNotificationTypeCommand(
	Guid UserId,
	NotificationType? NewNotificationType
) : IRequest<Result<Guid, AppException>>, IAuthorizable;
