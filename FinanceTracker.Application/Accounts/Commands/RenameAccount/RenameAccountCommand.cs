using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed record RenameAccountCommand(
	Guid UserId,
	Guid AccountId,
	string NewName
) : IRequest, IAuthorizable;