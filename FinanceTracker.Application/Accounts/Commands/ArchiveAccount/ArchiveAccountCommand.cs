using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.ArchiveAccount;

public sealed record ArchiveAccountCommand(
	Guid UserId,
	Guid AccountId
) : IRequest, IAuthorizable;