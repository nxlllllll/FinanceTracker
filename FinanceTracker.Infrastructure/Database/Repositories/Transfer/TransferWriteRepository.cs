using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferWriteRepository(
	FinanceTrackerContext context,
	IOperationWriteRepository operationRepository
) : ITransferWriteRepository
{
	public async Task CreateAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default)
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
			RateStatus = transfer.RateStatus,
			RateStatusChangedAt = transfer.RateStatusChangedAt,
			Description = transfer.Description,
			OccurredAt = transfer.OccurredAt,
			CreatedAt = transfer.CreatedAt,
			Status = transfer.Status,
			RowVersion = 0
		}, cancellationToken: ct);

		await operationRepository.InsertTransferAsync(transfer: transfer, ct: ct);
	}

	public async Task SaveRateResolutionAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default)
	{
		int affected = await context.Transfers.Where(predicate: t => t.Id == transfer.Id && t.RowVersion == transfer.RowVersion)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: transfer.ExchangeRate)
					.SetProperty(propertyExpression: t => t.AmountTo, valueExpression: transfer.AmountTo.Amount)
					.SetProperty(propertyExpression: t => t.RateStatus, valueExpression: transfer.RateStatus)
					.SetProperty(propertyExpression: t => t.RateStatusChangedAt, valueExpression: transfer.RateStatusChangedAt)
					.SetProperty(propertyExpression: t => t.RowVersion, valueExpression: transfer.RowVersion + 1),
				cancellationToken: ct
			);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transfer {transfer.Id} was modified by another request.", id: transfer.Id);

		await operationRepository.UpdateTransferAmountToAsync(
			transferId: transfer.Id,
			userId: transfer.UserId,
			amountTo: transfer.AmountTo.Amount,
			ct: ct
		);
	}

	public async Task SaveStatusAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default)
	{
		int affected = await context.Transfers.Where(predicate: t => t.Id == transfer.Id && t.RowVersion == transfer.RowVersion)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(propertyExpression: t => t.Status, valueExpression: transfer.Status)
					.SetProperty(propertyExpression: t => t.RateStatus, valueExpression: transfer.RateStatus)
					.SetProperty(propertyExpression: t => t.RateStatusChangedAt, valueExpression: transfer.RateStatusChangedAt)
					.SetProperty(propertyExpression: t => t.RowVersion, valueExpression: transfer.RowVersion + 1),
				cancellationToken: ct
			);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transfer {transfer.Id} was modified by another request.", id: transfer.Id);

		await operationRepository.UpdateTransferStatusAsync(
			transferId: transfer.Id,
			userId: transfer.UserId,
			status: transfer.Status,
			ct: ct
		);
	}
}
