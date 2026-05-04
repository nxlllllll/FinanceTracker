using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.RenameAccount;

public sealed record RenameAccountCommand(
	Guid UserId,
	Guid AccountId,
	string NewName
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;