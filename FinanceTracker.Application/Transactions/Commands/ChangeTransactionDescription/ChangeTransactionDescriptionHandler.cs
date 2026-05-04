using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, Transaction, Guid>
{
	public async Task<Guid> HandleAsync(
		ChangeTransactionDescriptionCommand command,
		Transaction transaction,
		CancellationToken ct = default
	)
	{
		if (transaction.Description == command.Description)
			return transaction.Id;
		
		transaction.ChangeDescription(description: command.Description);
		
		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			description: command.Description,
			ct: ct
		);
		
		return transaction.Id;
	}
}