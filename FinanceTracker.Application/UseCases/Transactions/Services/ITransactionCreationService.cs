using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transactions.Services;

public interface ITransactionCreationService
{
	Task<Result<Guid, DomainException>> CreateAsync(
		CreateTransactionCommand command,
		Account account,
		CancellationToken ct = default
	);
}