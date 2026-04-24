namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferWriteRepository
{
	Task CreateAsync(
		Guid transferId,
		Guid userId,
		Guid fromAccountId,
		Guid toAccountId,
		decimal amountFrom,
		decimal amountTo,
		decimal exchangeRate,
		string? description,
		DateTime occurredAt,
		bool isRatePending,
		CancellationToken ct = default
	);
}