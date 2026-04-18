using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
	Guid UserId,
	string Name,
	string AccountType,
	string Currency,
	decimal InitialBalance
) : IRequest<Guid>;