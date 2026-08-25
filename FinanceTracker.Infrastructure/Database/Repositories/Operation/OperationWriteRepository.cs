using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Operation;

public sealed class OperationWriteRepository(FinanceTrackerContext context) : IOperationWriteRepository
{
	private const string Transaction = nameof(Transaction);
	private const string Transfer = nameof(Transfer);

	private static string Invert(DirectionType direction) => direction switch
	{
		DirectionType.Debit => nameof(DirectionType.Credit).ToLowerInvariant(),
		DirectionType.Credit => nameof(DirectionType.Debit).ToLowerInvariant(),
		_ => throw new InvalidTransactionDirectionException(message: "Unknown direction type.")
	};

	public async Task InsertTransactionAsync(
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		await context.Operations.AddAsync(entity: new OperationEntity
		{
			Id = transaction.Id,
			UserId = transaction.UserId,
			Type = Transaction,
			OccurredAt = transaction.OccurredAt,
			Description = transaction.Description,
			AccountId = transaction.AccountId,
			CategoryId = transaction.CategoryId,
			Amount = transaction.Amount.Amount,
			CurrencyCode = transaction.Amount.Currency.Value,
			DirectionType = transaction.Direction.ToString().ToLowerInvariant(),
			IsExcluded = transaction.IsExcluded,
			IsReverted = false,
			ReversalOfId = null,
			FromAccountId = null,
			ToAccountId = null,
			AmountFrom = null,
			CurrencyFrom = null,
			AmountTo = null,
			CurrencyTo = null,
			Status = null
		}, cancellationToken: ct);
	}

	public async Task InsertTransferAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default)
	{
		await context.Operations.AddAsync(entity: new OperationEntity
		{
			Id = transfer.Id,
			UserId = transfer.UserId,
			Type = Transfer,
			OccurredAt = transfer.OccurredAt,
			Description = transfer.Description,
			AccountId = null,
			CategoryId = null,
			Amount = null,
			CurrencyCode = null,
			DirectionType = null,
			IsExcluded = null,
			IsReverted = false,
			ReversalOfId = null,
			FromAccountId = transfer.FromAccountId,
			ToAccountId = transfer.ToAccountId,
			AmountFrom = transfer.AmountFrom.Amount,
			CurrencyFrom = transfer.AmountFrom.Currency.Value,
			AmountTo = transfer.AmountTo.Amount,
			CurrencyTo = transfer.AmountTo.Currency.Value,
			Status = transfer.Status.ToCode()
		}, cancellationToken: ct);
	}

	public async Task InsertTransactionReversalAsync(
		Guid reversalId,
		Core.Domains.Transaction.Transaction transaction,
		DateTimeOffset occurredAt,
		CancellationToken ct = default)
	{
		await context.Operations.AddAsync(entity: new OperationEntity
		{
			Id = reversalId,
			UserId = transaction.UserId,
			Type = Transaction,
			OccurredAt = occurredAt,
			Description = transaction.Description,
			AccountId = transaction.AccountId,
			CategoryId = transaction.CategoryId,
			Amount = transaction.Amount.Amount,
			CurrencyCode = transaction.Amount.Currency.Value,
			DirectionType = Invert(direction: transaction.Direction),
			IsExcluded = transaction.IsExcluded,
			IsReverted = false,
			ReversalOfId = transaction.Id,
			FromAccountId = null,
			ToAccountId = null,
			AmountFrom = null,
			CurrencyFrom = null,
			AmountTo = null,
			CurrencyTo = null,
			Status = null
		}, cancellationToken: ct);

		await context.Operations.Where(predicate: o => o.Id == transaction.Id && o.UserId == transaction.UserId && o.Type == Transaction).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.IsReverted, valueExpression: true),
			cancellationToken: ct
		);
	}

	public async Task UpdateTransactionCategoryAsync(
		Guid transactionId,
		Guid userId,
		Guid categoryId,
		CancellationToken ct = default)
	{
		await context.Operations.Where(predicate: o => o.Id == transactionId && o.UserId == userId && o.Type == Transaction).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.CategoryId, valueExpression: categoryId),
			cancellationToken: ct
		);
	}

	public async Task UpdateTransactionDescriptionAsync(
		Guid transactionId,
		Guid userId,
		string? description,
		CancellationToken ct = default)
	{
		await context.Operations.Where(predicate: o => o.Id == transactionId && o.UserId == userId && o.Type == Transaction).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.Description, valueExpression: description),
			cancellationToken: ct
		);
	}

	public async Task UpdateTransactionExclusionAsync(
		Guid transactionId,
		Guid userId,
		bool isExcluded,
		CancellationToken ct = default)
	{
		await context.Operations.Where(predicate: o => o.Id == transactionId && o.UserId == userId && o.Type == Transaction).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.IsExcluded, valueExpression: isExcluded),
			cancellationToken: ct
		);
	}

	public async Task UpdateTransferStatusAsync(
		Guid transferId,
		Guid userId,
		TransferStatus status,
		CancellationToken ct = default)
	{
		await context.Operations.Where(predicate: o => o.Id == transferId && o.UserId == userId && o.Type == Transfer).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.Status, valueExpression: status.ToCode()),
			cancellationToken: ct
		);
	}

	public async Task UpdateTransferAmountToAsync(
		Guid transferId,
		Guid userId,
		decimal amountTo,
		CancellationToken ct = default)
	{
		await context.Operations.Where(predicate: o => o.Id == transferId && o.UserId == userId && o.Type == Transfer).ExecuteUpdateAsync(
			setPropertyCalls: b => b.SetProperty(propertyExpression: o => o.AmountTo, valueExpression: amountTo),
			cancellationToken: ct
		);
	}
}
