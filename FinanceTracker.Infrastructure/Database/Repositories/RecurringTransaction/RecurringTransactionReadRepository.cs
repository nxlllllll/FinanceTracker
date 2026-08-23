using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionReadRepository(
	FinanceTrackerContext context
) : IRecurringTransactionReadRepository
{
	public async Task<RecurringTransactionReadModel?> GetByIdAsync(
		Guid recurringTransactionId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.Id == recurringTransactionId && r.UserId == userId)
			.Join(
				inner: context.Users,
				outerKeySelector: r => r.UserId,
				innerKeySelector: u => u.Id,
				resultSelector: (r, u) => new RecurringTransactionReadModel(
					Id: r.Id,
					UserId: r.UserId,
					AccountId: r.AccountId,
					CategoryId: r.CategoryId,
					Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
					Direction: r.Direction,
					DayOfMonth: r.DayOfMonth,
					NextDueAtUtc: r.NextDueAtUtc,
					TimeZone: u.TimeZoneId,
					Description: r.Description,
					IsActive: r.IsActive,
					RowVersion: r.RowVersion,
					LastExecutedAt: r.LastExecutedAt,
					LastMissedAt: r.LastMissedAt,
					CreatedAt: r.CreatedAt
				)
			).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<RecurringTransactionReadModel>> GetByUserIdAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<RecurringTransactionEntity> query = context.RecurringTransactions.AsNoTracking()
															.Where(predicate: r => r.UserId == userId);

		if (cursorCreatedAt is not null && cursorId is not null)
			query = query.Where(predicate: r => r.CreatedAt < cursorCreatedAt.Value || (r.CreatedAt == cursorCreatedAt.Value && r.Id < cursorId.Value));

		List<RecurringTransactionReadModel> items = await query
			.OrderByDescending(keySelector: r => r.CreatedAt)
			.ThenByDescending(keySelector: r => r.Id)
			.Take(count: pageSize + 1)
			.Join(
				inner: context.Users,
				outerKeySelector: r => r.UserId,
				innerKeySelector: u => u.Id,
				resultSelector: (r, u) => new { Recurring = r, User = u }
			)
			.OrderByDescending(keySelector: x => x.Recurring.CreatedAt)
			.ThenByDescending(keySelector: x => x.Recurring.Id)
			.Select(selector: x => new RecurringTransactionReadModel(
				Id: x.Recurring.Id,
				UserId: x.Recurring.UserId,
				AccountId: x.Recurring.AccountId,
				CategoryId: x.Recurring.CategoryId,
				Amount: Money.Reconstitute(amount: x.Recurring.Amount, currency: x.Recurring.Currency),
				Direction: x.Recurring.Direction,
				DayOfMonth: x.Recurring.DayOfMonth,
				NextDueAtUtc: x.Recurring.NextDueAtUtc,
				TimeZone: x.User.TimeZoneId,
				Description: x.Recurring.Description,
				IsActive: x.Recurring.IsActive,
				RowVersion: x.Recurring.RowVersion,
				LastExecutedAt: x.Recurring.LastExecutedAt,
				LastMissedAt: x.Recurring.LastMissedAt,
				CreatedAt: x.Recurring.CreatedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;

		if (hasNextPage)
			items.RemoveAt(index: pageSize);

		RecurringTransactionReadModel? last = hasNextPage ? items[^1] : null;

		return new PagedResult<RecurringTransactionReadModel>(
			Items: items,
			HasNextPage: hasNextPage,
			NextCursorDate: last?.CreatedAt,
			NextCursorId: last?.Id
		);
	}

	public async Task<IReadOnlyList<RecurringTransactionReadModel>> GetDueAsync(
		DateTimeOffset asOf,
		CancellationToken ct = default
	) => await DueQuery(bound: asOf, onlyUnescalated: false).ToListAsync(cancellationToken: ct);

	public async Task<IReadOnlyList<RecurringTransactionReadModel>> GetOverdueAsync(
		DateTimeOffset before,
		CancellationToken ct = default
	) => await DueQuery(bound: before, onlyUnescalated: true).ToListAsync(cancellationToken: ct);

	private IQueryable<RecurringTransactionReadModel> DueQuery(DateTimeOffset bound, bool onlyUnescalated)
	{
		IQueryable<RecurringTransactionEntity> query = context.RecurringTransactions.AsNoTracking()
															.Where(predicate: r => r.IsActive && r.NextDueAtUtc <= bound);

		if (onlyUnescalated)
			query = query.Where(predicate: r => r.LastMissedAt == null || r.LastMissedAt < r.NextDueAtUtc);

		return query.Join(
			inner: context.Users,
			outerKeySelector: r => r.UserId,
			innerKeySelector: u => u.Id,
			resultSelector: (r, u) => new { Recurring = r, User = u }
		)
		.OrderBy(keySelector: x => x.Recurring.NextDueAtUtc)
		.Select(selector: x => new RecurringTransactionReadModel(
			Id: x.Recurring.Id,
			UserId: x.Recurring.UserId,
			AccountId: x.Recurring.AccountId,
			CategoryId: x.Recurring.CategoryId,
			Amount: Money.Reconstitute(amount: x.Recurring.Amount, currency: x.Recurring.Currency),
			Direction: x.Recurring.Direction,
			DayOfMonth: x.Recurring.DayOfMonth,
			NextDueAtUtc: x.Recurring.NextDueAtUtc,
			TimeZone: x.User.TimeZoneId,
			Description: x.Recurring.Description,
			IsActive: x.Recurring.IsActive,
			RowVersion: x.Recurring.RowVersion,
			LastExecutedAt: x.Recurring.LastExecutedAt,
			LastMissedAt: x.Recurring.LastMissedAt,
			CreatedAt: x.Recurring.CreatedAt
		));
	}
}
