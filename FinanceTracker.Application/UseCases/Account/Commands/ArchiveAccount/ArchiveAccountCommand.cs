using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;

public sealed record ArchiveAccountCommand(
	Guid UserId,
	Guid AccountId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
