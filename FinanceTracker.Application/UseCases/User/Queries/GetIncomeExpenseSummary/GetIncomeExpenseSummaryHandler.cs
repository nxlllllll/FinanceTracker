using FinanceTracker.Application.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;

public sealed class GetIncomeExpenseSummaryHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetIncomeExpenseSummaryQuery, Result<IncomeExpenseSummary, AppException>>
{
	public async Task<Result<IncomeExpenseSummary, AppException>> Handle(
		GetIncomeExpenseSummaryQuery query,
		CancellationToken ct = default)
	{
		UserReadModel? user = await userQueryRepository.GetByIdAsync(userId: query.UserId, ct: ct);
		if (user is null)
			return Result<IncomeExpenseSummary, AppException>.Failure(error: new NotFoundException(message: "User not found.", id: query.UserId));

		(decimal income, decimal expense) = await userQueryRepository.GetIncomeExpenseSummaryAsync(
			userId: query.UserId,
			period: query.Period,
			ct: ct
		);

		return Result<IncomeExpenseSummary, AppException>.Success(value: new IncomeExpenseSummary(
			Income: income,
			Expense: expense,
			Currency: user.BaseCurrency,
			Period: query.Period
		));
	}
}
