using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionCreationService transactionCreationService
) : IAuthorizedHandler<CreateTransactionCommand, Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateTransactionCommand command,
		Account account,
		CancellationToken ct = default
	) => await transactionCreationService.CreateAsync(command: command, account: account, ct: ct);
}