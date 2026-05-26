using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;

public sealed record RenameAccountCommand(
	Guid UserId,
	Guid AccountId,
	Name NewName
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
