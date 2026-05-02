using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Application.Transactions.Services;

public interface ITransactionCreationService
{
	Task<Guid> CreateAsync(
		CreateTransactionCommand command,
		Account account,
		CancellationToken ct = default
	);
}