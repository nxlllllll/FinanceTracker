using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
			IsRatePending = transfer.IsRatePending,
			Status = transfer.Status,
			RowVersion = 0
		}, cancellationToken: ct);
	}

	public async Task UpdateRateAsync(
		Guid transferId,
		decimal newRate,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transfers.Where(predicate: t => t.Id == transferId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: newRate)
				.SetProperty(propertyExpression: t => t.IsRatePending, valueExpression: false)
				.SetProperty(propertyExpression: t => t.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transfer {transferId} was modified by another request.", id: transferId);
	}

	public async Task UpdateStatusAsync(
		Guid transferId,
		TransferStatus status,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transfers.Where(predicate: t => t.Id == transferId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: t => t.Status, valueExpression: status)
				.SetProperty(propertyExpression: t => t.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transfer {transferId} was modified by another request.", id: transferId);
	}
}