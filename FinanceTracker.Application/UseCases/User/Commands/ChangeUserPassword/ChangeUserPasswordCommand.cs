using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;

public sealed record ChangeUserPasswordCommand(
	Guid UserId,
	Guid CurrentSessionId,
	string CurrentPassword,
	string NewPassword
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
