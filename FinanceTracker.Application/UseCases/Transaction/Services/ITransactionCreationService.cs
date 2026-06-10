using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Services;

/// <summary>
/// Encapsulates the side-effectful logic of creating a transaction:
/// account debit/credit, category total update, budget progress update,
/// and currency conversion. Extracted from the handler to keep
/// <c>CreateTransactionHandler</c> focused on authorization and orchestration.
/// </summary>
public interface ITransactionCreationService
{
	/// <summary>
	/// Creates the transaction record, applies it to the account balance,
	/// updates category totals and budget progress, and handles exchange rate conversion.
	/// All operations are executed within the caller's transaction scope.
	/// </summary>
	Task<Result<Guid, DomainException>> CreateAsync(
		CreateTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default
	);
}