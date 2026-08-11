using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, Result<PagedResult<RecurringTransactionReadModel>, AppException>>
{
	public async Task<Result<PagedResult<RecurringTransactionReadModel>, AppException>> Handle(
		GetRecurringTransactionsQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<RecurringTransactionReadModel>, AppException>.Success(value: await recurringTransactionReadRepository.GetByUserIdAsync(
			userId: query.UserId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
