using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionQuery, RecurringTransactionReadModel>
{
	public async Task<RecurringTransactionReadModel> Handle(
		GetRecurringTransactionQuery query,
		CancellationToken ct = default)
	{
		RecurringTransactionReadModel? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: query.RecurringTransactionId, ct: ct);

		if (recurringTransaction is null || recurringTransaction.UserId != query.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId);

		return recurringTransaction;
	}
}