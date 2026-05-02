using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transactions.Services;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

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