using FinanceTracker.Application.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;

public sealed class GetIncomeExpenseSummaryHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetIncomeExpenseSummaryQuery, IncomeExpenseSummary>
{
	public async Task<IncomeExpenseSummary> Handle(
		GetIncomeExpenseSummaryQuery query,
		CancellationToken ct = default)
	{
		UserReadModel user = await userQueryRepository.GetByIdAsync(userId: query.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: query.UserId);

		(decimal income, decimal expense) = await userQueryRepository.GetIncomeExpenseSummaryAsync(
			userId: query.UserId,
			period: query.Period,
			ct: ct
		);

		return new IncomeExpenseSummary(
			Income: income,
			Expense: expense,
			Currency: user.BaseCurrency,
			Period: query.Period
		);
	}
}
