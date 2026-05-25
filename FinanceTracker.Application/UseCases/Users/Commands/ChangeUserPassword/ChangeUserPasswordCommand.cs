using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;

public sealed record ChangeUserPasswordCommand(
	Guid UserId,
	string NewPassword
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
