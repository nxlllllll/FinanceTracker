using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;

public sealed record ArchiveAccountCommand(
	Guid UserId,
	Guid AccountId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;