using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionWriteRepository(
	FinanceTrackerContext context,
	IOperationWriteRepository operationRepository
) : ITransactionWriteRepository
{
	public async Task CreateAsync(
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		await context.Transactions.AddAsync(entity: new TransactionEntity
		{
			Id = transaction.Id,
			AccountId = transaction.AccountId,
			UserId = transaction.UserId,
			CategoryId = transaction.CategoryId,
			Amount = transaction.Amount.Amount,
			Currency = transaction.Amount.Currency,
			BaseCurrency = transaction.BaseCurrency,
			Direction = transaction.Direction,
			ExchangeRate = transaction.ExchangeRate,
			RateStatus = transaction.RateStatus,
			RateStatusChangedAt = transaction.RateStatusChangedAt,
			Description = transaction.Description,
			IsExcluded = false,
			RowVersion = 0,
			OccurredAt = transaction.OccurredAt
		}, cancellationToken: ct);

		await operationRepository.InsertTransactionAsync(transaction: transaction, ct: ct);
	}

	public async Task ChangeCategoryAsync(
		Guid transactionId,
		Guid userId,
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.CategoryId, valueExpression: categoryId)
				.SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);

		await operationRepository.UpdateTransactionCategoryAsync(
			transactionId: transactionId,
			userId: userId,
			categoryId: categoryId,
			ct: ct
		);
	}

	public async Task ChangeDescriptionAsync(
		Guid transactionId,
		Guid userId,
		string? description,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.Description, valueExpression: description)
				.SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);

		await operationRepository.UpdateTransactionDescriptionAsync(
			transactionId: transactionId,
			userId: userId,
			description: description,
			ct: ct
		);
	}

	public async Task IncludeAsync(
		Guid transactionId,
		Guid userId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.IsExcluded, valueExpression: false)
				.SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);

		await operationRepository.UpdateTransactionExclusionAsync(
			transactionId: transactionId,
			userId: userId,
			isExcluded: false,
			ct: ct
		);
	}

	public async Task ExcludeAsync(
		Guid transactionId,
		Guid userId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.IsExcluded, valueExpression: true)
				.SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);

		await operationRepository.UpdateTransactionExclusionAsync(
			transactionId: transactionId,
			userId: userId,
			isExcluded: true,
			ct: ct
		);
	}

	public async Task SaveRateResolutionAsync(
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		int affected = await context.Transactions.Where(predicate: t => t.Id == transaction.Id && t.RowVersion == transaction.RowVersion)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: transaction.ExchangeRate)
					.SetProperty(propertyExpression: t => t.RateStatus, valueExpression: transaction.RateStatus)
					.SetProperty(propertyExpression: t => t.RateStatusChangedAt, valueExpression: transaction.RateStatusChangedAt)
					.SetProperty(propertyExpression: t => t.RowVersion, valueExpression: transaction.RowVersion + 1),
				cancellationToken: ct
			);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Transaction {transaction.Id} was modified by another request.", id: transaction.Id);
	}
}
