using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
	Guid UserId,
	string Name,
	AccountType Type,
	string Currency,
	decimal InitialBalance
) : IRequest<Guid>;