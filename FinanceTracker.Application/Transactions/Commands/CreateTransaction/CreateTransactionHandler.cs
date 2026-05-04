using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transactions.Services;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionCreationService transactionCreationService
) : IAuthorizedHandler<CreateTransactionCommand, Account, Guid>
{
	public async Task<Guid> HandleAsync(
		CreateTransactionCommand command,
		Account account,
		CancellationToken ct = default
	) => await transactionCreationService.CreateAsync(command: command, account: account, ct: ct);
}