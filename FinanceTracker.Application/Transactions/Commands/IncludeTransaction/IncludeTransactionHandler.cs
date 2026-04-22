using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed class IncludeTransactionHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository
) : IRequestHandler<IncludeTransactionCommand>
{
	public async Task Handle(
		IncludeTransactionCommand command,
		CancellationToken ct = default)
	{
		bool exists = await transactionReadRepository.ExistsAsync(
			transactionId: command.TransactionId, ct: ct
		);

		if (!exists)
			throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		await transactionWriteRepository.IncludeAsync(
			transactionId: command.TransactionId,
			ct: ct
		);
	}
}