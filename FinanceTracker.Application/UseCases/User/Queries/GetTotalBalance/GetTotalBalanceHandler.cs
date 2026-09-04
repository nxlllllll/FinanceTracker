using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;

public sealed class GetTotalBalanceHandler(
	IUserQueryRepository userQueryRepository,
	IDateProvider dateProvider
) : IRequestHandler<GetTotalBalanceQuery, Result<TotalBalanceReadModel, AppException>>
{
	public async Task<Result<TotalBalanceReadModel, AppException>> Handle(
		GetTotalBalanceQuery query,
		CancellationToken ct = default)
	{
		UserReadModel? user = await userQueryRepository.GetByIdAsync(userId: query.UserId, ct: ct);
		if (user is null)
			return Result<TotalBalanceReadModel, AppException>.Failure(error: new NotFoundException(message: "User not found.", id: query.UserId));

		TotalBalanceReadModel balance = await userQueryRepository.GetTotalBalanceAsync(
			userId: query.UserId,
			baseCurrency: user.BaseCurrency,
			date: dateProvider.UtcToday,
			ct: ct
		);

		return Result<TotalBalanceReadModel, AppException>.Success(value: balance);
	}
}
