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
			AmountFrom = transfer.AmountFrom,
			CurrencyFrom = transfer.CurrencyFrom,
			AmountTo = transfer.AmountTo,
			CurrencyTo = transfer.CurrencyTo,
			ExchangeRate = transfer.ExchangeRate,
			IsExcluded = transfer.IsExcluded,
			Description = transfer.Description,
			OccurredAt = transfer.OccurredAt,
			IsRatePending = transfer.IsRatePending
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}