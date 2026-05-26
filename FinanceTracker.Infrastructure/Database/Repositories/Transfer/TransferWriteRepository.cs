using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferWriteRepository(
	FinanceTrackerContext context
) : ITransferWriteRepository
{
	public async Task CreateAsync(Core.Domains.Transfer.Transfer transfer, CancellationToken ct = default)
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
	
	public async Task UpdateRateAsync(
	    Guid transferId,
	    decimal newRate,
	    CancellationToken ct = default)
	{
	    await context.Transfers.Where(predicate: t => t.Id == transferId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: newRate)
				.SetProperty(propertyExpression: t => t.IsRatePending, valueExpression: false),
            cancellationToken: ct
        );
	}
}
