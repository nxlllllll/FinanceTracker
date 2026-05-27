using FinanceTracker.Application.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;

public sealed class GetIncomeExpenseSummaryHandler(
	IUserReadRepository userReadRepository
) : IRequestHandler<GetIncomeExpenseSummaryQuery, IncomeExpenseSummary>
{
	public async Task<IncomeExpenseSummary> Handle(
		GetIncomeExpenseSummaryQuery query,
		CancellationToken ct = default)
	{
		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct)
		?? throw new NotFoundException(message: "User not found.", id: query.UserId);

		(decimal income, decimal expense) = await userReadRepository.GetIncomeExpenseSummaryAsync(
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