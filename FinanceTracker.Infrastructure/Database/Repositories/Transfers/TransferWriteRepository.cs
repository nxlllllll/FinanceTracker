using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfers;

public sealed class TransferWriteRepository(
	FinanceTrackerContext context
) : ITransferWriteRepository
{
	public async Task CreateAsync(
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
		CancellationToken ct = default)
	{
		await context.Transfers.AddAsync(entity: new TransferEntity()
		{
			Id = transferId,
			UserId = userId,
			FromAccountId = fromAccountId,
			ToAccountId = toAccountId,
			AmountFrom = amountFrom,
			AmountTo = amountTo,
			ExchangeRate = exchangeRate,
			IsExcluded = false,
			Description = description,
			OccurredAt = occurredAt,
			IsRatePending = isRatePending
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}