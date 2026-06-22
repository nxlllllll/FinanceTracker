using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionReadRepository(FinanceTrackerContext context) : IRecurringTransactionReadRepository
{
	public async Task<RecurringTransactionReadModel?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default)
	{
		return await context.RecurringTransactions.AsNoTracking().Where(predicate: r => r.Id == recurringTransactionId)
			.Select(selector: r => new RecurringTransactionReadModel(
				Id: r.Id,
				UserId: r.UserId,
				AccountId: r.AccountId,
				CategoryId: r.CategoryId,
				Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				Direction: r.Direction,
				DayOfMonth: r.DayOfMonth,
				Description: r.Description,
				IsActive: r.IsActive,
				RowVersion: r.RowVersion,
				LastExecutedAt: r.LastExecutedAt,
				LastMissedAt: r.LastMissedAt,
				CreatedAt: r.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

    public async Task<PagedResult<RecurringTransactionReadModel>> GetByUserIdAsync(
        Guid userId,
        DateTimeOffset? cursorCreatedAt = null,
        Guid? cursorId = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        IQueryable<Context.RecurringTransaction.RecurringTransactionEntity> query = context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.UserId == userId);

        if (cursorCreatedAt is not null && cursorId is not null)
            query = query.Where(predicate: r => r.CreatedAt < cursorCreatedAt.Value || (r.CreatedAt == cursorCreatedAt.Value && r.Id < cursorId.Value));

        List<RecurringTransactionReadModel> items = await query.OrderByDescending(keySelector: r => r.CreatedAt)
			.ThenByDescending(keySelector: r => r.Id)
			.Take(count: pageSize + 1)
			.Select(selector: r => new RecurringTransactionReadModel(
				Id: r.Id,
				UserId: r.UserId,
				AccountId: r.AccountId,
				CategoryId: r.CategoryId,
				Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				Direction: r.Direction,
				DayOfMonth: r.DayOfMonth,
				Description: r.Description,
				IsActive: r.IsActive,
				RowVersion: r.RowVersion,
				LastExecutedAt: r.LastExecutedAt,
				LastMissedAt: r.LastMissedAt,
				CreatedAt: r.CreatedAt
			)).ToListAsync(cancellationToken: ct);

        bool hasNextPage = items.Count > pageSize;
        if (hasNextPage)
            items.RemoveAt(index: items.Count - 1);

        RecurringTransactionReadModel? last = items.Count > 0 ? items[^1] : null;

        return new PagedResult<RecurringTransactionReadModel>(
            Items: items.AsReadOnly(),
            HasNextPage: hasNextPage,
            NextCursorDate: hasNextPage ? last?.CreatedAt : null,
            NextCursorId: hasNextPage ? last?.Id : null
        );
    }

	public async Task<IReadOnlyList<RecurringTransactionReadModel>> GetDueTodayAsync(
	    int dayOfMonth,
	    int daysInCurrentMonth,
	    DateTimeOffset currentMonthStart,
	    CancellationToken ct = default)
	{
	    bool isLastDayOfMonth = dayOfMonth == daysInCurrentMonth;

	    IQueryable<Context.RecurringTransaction.RecurringTransactionEntity> notExecutedThisMonth = context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.IsActive && (r.LastExecutedAt == null || r.LastExecutedAt < currentMonthStart));

	    IQueryable<Context.RecurringTransaction.RecurringTransactionEntity> exactDayMatch = notExecutedThisMonth
			.Where(predicate: r => r.DayOfMonth == dayOfMonth);

	    IQueryable<Context.RecurringTransaction.RecurringTransactionEntity> query = isLastDayOfMonth
	        ? exactDayMatch.Concat(notExecutedThisMonth.Where(predicate: r => r.DayOfMonth > daysInCurrentMonth))
	        : exactDayMatch;

	    return await query.Select(selector: r => new RecurringTransactionReadModel(
	        Id: r.Id,
	        UserId: r.UserId,
	        AccountId: r.AccountId,
	        CategoryId: r.CategoryId,
	        Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
	        Direction: r.Direction,
	        DayOfMonth: r.DayOfMonth,
	        Description: r.Description,
	        IsActive: r.IsActive,
	        RowVersion: r.RowVersion,
	        LastExecutedAt: r.LastExecutedAt,
			LastMissedAt: r.LastMissedAt,
	        CreatedAt: r.CreatedAt
	    )).ToListAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<RecurringTransactionReadModel>> GetMissedThisMonthAsync(
		int dayOfMonth,
		DateTimeOffset currentMonthStart,
		DateTimeOffset previousMonthStart,
		CancellationToken ct = default)
	{
		return await context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.IsActive &&
				(r.LastExecutedAt == null || r.LastExecutedAt < currentMonthStart) &&
				(r.LastMissedAt == null || r.LastMissedAt < currentMonthStart) &&
				(
					r.DayOfMonth < dayOfMonth || (r.CreatedAt < previousMonthStart && 
					(r.LastExecutedAt == null || r.LastExecutedAt < previousMonthStart) &&
					(r.LastMissedAt == null || r.LastMissedAt < previousMonthStart))
				)
			).Select(selector: r => new RecurringTransactionReadModel(
				Id: r.Id,
				UserId: r.UserId,
				AccountId: r.AccountId,
				CategoryId: r.CategoryId,
				Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				Direction: r.Direction,
				DayOfMonth: r.DayOfMonth,
				Description: r.Description,
				IsActive: r.IsActive,
				RowVersion: r.RowVersion,
				LastExecutedAt: r.LastExecutedAt,
				LastMissedAt: r.LastMissedAt,
				CreatedAt: r.CreatedAt
			)).ToListAsync(cancellationToken: ct);
	}
}