using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed record UnarchiveAccountCommand(
	Guid UserId,
	Guid AccountId
) : IRequest<Guid>, IAuthorizable;