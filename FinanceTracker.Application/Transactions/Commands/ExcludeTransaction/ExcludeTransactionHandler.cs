using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository
) : IRequestHandler<ExcludeTransactionCommand>
{
	public async Task Handle(
		ExcludeTransactionCommand command,
		CancellationToken ct = default)
	{
		bool exists = await transactionReadRepository.ExistsAsync(
			transactionId: command.TransactionId, ct: ct
		);

		if (!exists)
			throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		await transactionWriteRepository.ExcludeAsync(
			transactionId: command.TransactionId,
			ct: ct
		);
	}
}