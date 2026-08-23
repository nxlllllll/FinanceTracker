using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Core.Repositories.Operation;

public interface IOperationWriteRepository
{
	Task InsertTransactionAsync(
		Domains.Transaction.Transaction transaction,
		CancellationToken ct = default
	);

	Task InsertTransferAsync(
		Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);

	Task UpdateTransactionCategoryAsync(
		Guid transactionId,
		Guid userId,
		Guid categoryId,
		CancellationToken ct = default
	);

	Task UpdateTransactionDescriptionAsync(
		Guid transactionId,
		Guid userId,
		string? description,
		CancellationToken ct = default
	);

	Task UpdateTransactionExclusionAsync(
		Guid transactionId,
		Guid userId,
		bool isExcluded,
		CancellationToken ct = default
	);

	Task UpdateTransferStatusAsync(
		Guid transferId,
		Guid userId,
		TransferStatus status,
		CancellationToken ct = default
	);

	Task UpdateTransferAmountToAsync(
		Guid transferId,
		Guid userId,
		decimal amountTo,
		CancellationToken ct = default
	);
}
