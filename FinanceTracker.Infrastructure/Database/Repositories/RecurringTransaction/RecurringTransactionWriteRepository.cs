using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IRecurringTransactionWriteRepository
{
	public async Task CreateAsync(
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		DateTimeOffset now = dateProvider.UtcNow;

		await context.RecurringTransactions.AddAsync(entity: new RecurringTransactionEntity()
		{
			Id = recurringTransaction.Id,
			UserId = recurringTransaction.UserId,
			AccountId = recurringTransaction.AccountId,
			CategoryId = recurringTransaction.CategoryId,
			Amount = recurringTransaction.Amount.Amount,
			Currency = recurringTransaction.Amount.Currency,
			Direction = recurringTransaction.Direction,
			DayOfMonth = recurringTransaction.DayOfMonth,
			NextDueAtUtc = recurringTransaction.NextDueAtUtc,
			Description = recurringTransaction.Description,
			IsActive = true,
			LastExecutedAt = null,
			LastMissedAt = null,
			RowVersion = 0,
			CreatedAt = now
		}, cancellationToken: ct);
	}

	public async Task ChangeAmountAsync(
		Guid recurringTransactionId,
		decimal amount,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.Amount, valueExpression: amount)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task ChangeCurrencyAsync(
		Guid recurringTransactionId,
		Core.ValueObjects.Currency currency,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.Currency, valueExpression: currency)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task ChangeDayOfMonthAsync(
		Guid recurringTransactionId,
		int dayOfMonth,
		DateTimeOffset nextDueAtUtc,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.DayOfMonth, valueExpression: dayOfMonth)
				.SetProperty(propertyExpression: r => r.NextDueAtUtc, valueExpression: nextDueAtUtc)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task ActivateAsync(
		Guid recurringTransactionId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.IsActive, valueExpression: true)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task DeactivateAsync(
		Guid recurringTransactionId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.IsActive, valueExpression: false)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		await context.RecurringTransactions.Where(predicate: r => r.CategoryId == categoryId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: r => r.IsActive, valueExpression: false)
				.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: r => r.RowVersion + 1),
			cancellationToken: ct
		);
	}

	public async Task MarkExecutedAsync(
		Guid recurringTransactionId,
		DateTimeOffset executedAt,
		DateTimeOffset nextDueAtUtc,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(propertyExpression: r => r.LastExecutedAt, valueExpression: executedAt)
					.SetProperty(propertyExpression: r => r.NextDueAtUtc, valueExpression: nextDueAtUtc)
					.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
				cancellationToken: ct
			);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task MarkMissedAsync(
		Guid recurringTransactionId,
		DateTimeOffset missedAt,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId && r.RowVersion == expectedVersion)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(propertyExpression: r => r.LastMissedAt, valueExpression: missedAt)
					.SetProperty(propertyExpression: r => r.RowVersion, valueExpression: expectedVersion + 1),
				cancellationToken: ct
			);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"RecurringTransaction {recurringTransactionId} was modified by another request.", id: recurringTransactionId);
	}

	public async Task RescheduleAllForUserAsync(
		Guid userId,
		TimeZoneId timeZone,
		CancellationToken ct = default)
	{
		var operations = await context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.UserId == userId && r.IsActive)
			.Select(selector: r => new { r.Id, r.DayOfMonth, r.NextDueAtUtc })
			.ToListAsync(cancellationToken: ct);

		List<Guid> ids = [];
		List<DateTimeOffset> rescheduled = [];

		foreach (var operation in operations)
		{
			DateTimeOffset next = RecurringDueDate.Next(
				dayOfMonth: operation.DayOfMonth,
				timeZone: timeZone,
				after: operation.NextDueAtUtc.AddHours(hours: -36)
			);

			if (next == operation.NextDueAtUtc)
				continue;

			ids.Add(item: operation.Id);
			rescheduled.Add(item: next);
		}

		await context.RescheduleRecurringTransactionsAsync(ids: ids, nextDueAtUtc: rescheduled, ct: ct);
	}
}
