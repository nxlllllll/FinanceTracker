using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;

public sealed record ChangeUserEmailCommand(
	Guid UserId,
	string NewEmail
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;