using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed record UnarchiveAccountCommand(Guid AccountId) : IRequest;