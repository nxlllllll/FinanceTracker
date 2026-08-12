using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionQuery, Result<RecurringTransactionReadModel, AppException>>
{
	public async Task<Result<RecurringTransactionReadModel, AppException>> Handle(
		GetRecurringTransactionQuery query,
		CancellationToken ct = default)
	{
		RecurringTransactionReadModel? model = await recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: query.RecurringTransactionId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<RecurringTransactionReadModel, AppException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId));

		return Result<RecurringTransactionReadModel, AppException>.Success(value: model);
	}
}
