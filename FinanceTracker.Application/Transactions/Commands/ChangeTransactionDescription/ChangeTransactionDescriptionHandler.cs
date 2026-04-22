using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository
) : IRequestHandler<ChangeTransactionDescriptionCommand>
{
	public async Task Handle(
		ChangeTransactionDescriptionCommand command,
		CancellationToken ct = default)
	{
		bool exists = await transactionReadRepository.ExistsAsync(
			transactionId: command.TransactionId, ct: ct
		);

		if (!exists)
			throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			description: command.Description,
			ct: ct
		);
	}
}