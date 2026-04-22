using FinanceTracker.Core.Domains.Transaction.Events;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionWriteRepository(
	FinanceTrackerContext context
) : ITransactionWriteRepository
{
	private async Task ChangeTransactionProperty(
		Guid transactionId,
		Action<UpdateSettersBuilder<TransactionEntity>> changePropertyAction,
		CancellationToken ct = default)
	{
		await context.Transactions.Where(predicate: transaction => transaction.Id == transactionId).ExecuteUpdateAsync(
			setPropertyCalls: changePropertyAction,
			cancellationToken: ct
		);
	}
	
	public async Task CreateAsync(
		TransactionCreated @event,
		CancellationToken ct = default)
	{
		await context.Transactions.AddAsync(entity: new TransactionEntity()
		{
			Id = @event.TransactionId,
			AccountId = @event.AccountId,
			UserId = @event.UserId,
			CategoryId = @event.CategoryId,
			Amount = @event.Amount,
			Direction = @event.Direction,
			ExchangeRate = @event.ExchangeRate,
			Description = @event.Description,
			IsExcluded = false,
			OccurredAt = @event.OccurredAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task ChangeCategoryAsync(
		TransactionCategoryChanged @event,
		CancellationToken ct = default)
	{
		await ChangeTransactionProperty(
			transactionId: @event.TransactionId, 
			changePropertyAction: builder => 
				builder.SetProperty(propertyExpression: t => t.CategoryId, valueExpression: @event.CategoryId),
			ct: ct
		);
	}

	public async Task ChangeDescriptionAsync(
		TransactionDescriptionChanged @event,
		CancellationToken ct = default)
	{
		await ChangeTransactionProperty(
			transactionId: @event.TransactionId, 
			changePropertyAction: builder => 
				builder.SetProperty(propertyExpression: t => t.Description, valueExpression: @event.Description),
			ct: ct
		);
	}

	public async Task IncludeAsync(
		TransactionIncluded @event,
		CancellationToken ct = default)
	{
		await ChangeTransactionProperty(
			transactionId: @event.TransactionId, 
			changePropertyAction: builder => 
				builder.SetProperty(propertyExpression: t => t.IsExcluded, valueExpression: false),
			ct: ct
		);
	}

	public async Task ExcludeAsync(
		TransactionExcluded @event,
		CancellationToken ct = default)
	{
		await ChangeTransactionProperty(
			transactionId: @event.TransactionId, 
			changePropertyAction: builder => 
				builder.SetProperty(propertyExpression: t => t.IsExcluded, valueExpression: true),
			ct: ct
		);
	}
}