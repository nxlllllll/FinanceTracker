using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionQuery, Result<RecurringTransactionReadModel, DomainException>>
{
	public async Task<Result<RecurringTransactionReadModel, DomainException>> Handle(
		GetRecurringTransactionQuery query,
		CancellationToken ct = default)
	{
		RecurringTransactionReadModel? model = await recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: query.RecurringTransactionId,
			ct: ct
		);

		if (model is null)
			return Result<RecurringTransactionReadModel, DomainException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId));

		return Result<RecurringTransactionReadModel, DomainException>.Success(value: model);
	}
}