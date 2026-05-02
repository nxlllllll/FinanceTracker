using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfers;

public sealed class TransferWriteRepository(
	FinanceTrackerContext context
) : ITransferWriteRepository
{
	public async Task CreateAsync(Transfer transfer, CancellationToken ct = default)
	{
		await context.Transfers.AddAsync(entity: new TransferEntity
		{
			Id = transfer.Id,
			UserId = transfer.UserId,
			FromAccountId = transfer.FromAccountId,
			ToAccountId = transfer.ToAccountId,
			AmountFrom = transfer.AmountFrom.Amount,
			CurrencyFrom = transfer.AmountFrom.Currency,
			AmountTo = transfer.AmountTo.Amount,
			CurrencyTo = transfer.AmountTo.Currency,
			ExchangeRate = transfer.ExchangeRate,
			Description = transfer.Description,
			OccurredAt = transfer.OccurredAt,
			IsRatePending = transfer.IsRatePending
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}