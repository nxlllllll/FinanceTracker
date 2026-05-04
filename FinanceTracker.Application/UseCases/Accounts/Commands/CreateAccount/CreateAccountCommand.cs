using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
	Guid UserId,
	string Name,
	AccountType Type,
	string Currency,
	decimal InitialBalance
) : IRequest<Result<Guid, DomainException>>;