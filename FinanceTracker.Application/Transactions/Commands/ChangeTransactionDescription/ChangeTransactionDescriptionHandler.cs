using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, TransactionDto>
{
	public async Task HandleAsync(
		ChangeTransactionDescriptionCommand command,
		TransactionDto transaction,
		CancellationToken ct = default
	) => await transactionWriteRepository.ChangeDescriptionAsync(transactionId: command.TransactionId, description: command.Description, ct: ct);
}