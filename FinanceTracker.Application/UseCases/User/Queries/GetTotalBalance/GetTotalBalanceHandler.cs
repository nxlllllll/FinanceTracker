using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;

public sealed class GetTotalBalanceHandler(
	IUserReadRepository userReadRepository,
	IDateProvider dateProvider
) : IRequestHandler<GetTotalBalanceQuery, Money>
{
	public async Task<Money> Handle(
		GetTotalBalanceQuery query,
		CancellationToken ct = default)
	{
		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct)
		?? throw new NotFoundException(message: "User not found.", id: query.UserId);

		decimal balance = await userReadRepository.GetTotalBalanceAsync(
			userId: query.UserId,
			baseCurrency: user.BaseCurrency,
			date: dateProvider.UtcToday,
			ct: ct
		);

		return Money.Reconstitute(amount: balance, currency: user.BaseCurrency);
	}
}