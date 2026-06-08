using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferRepository(
	FinanceTrackerContext context
) : ITransferRepository
{
	public async Task<Core.Domains.Transfer.Transfer?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default)
	{
		TransferEntity? entity = await context.Transfers.AsNoTracking()
			.Where(predicate: t => t.Id == transferId)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (entity is null)
			return null;

		return Core.Domains.Transfer.Transfer.Reconstitute(
			id: entity.Id,
			userId: entity.UserId,
			fromAccountId: entity.FromAccountId,
			toAccountId: entity.ToAccountId,
			amountFrom: Money.Reconstitute(amount: entity.AmountFrom, currency: entity.CurrencyFrom),
			amountTo: Money.Reconstitute(amount: entity.AmountTo, currency: entity.CurrencyTo),
			exchangeRate: entity.ExchangeRate,
			isRatePending: entity.IsRatePending,
			status: entity.Status,
			description: entity.Description,
			occurredAt: entity.OccurredAt
		);
	}
}