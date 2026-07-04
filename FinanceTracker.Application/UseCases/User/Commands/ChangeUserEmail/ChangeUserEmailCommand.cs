using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;

public sealed record ChangeUserEmailCommand(
	Guid UserId,
	string NewEmail
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
