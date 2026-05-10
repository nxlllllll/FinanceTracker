using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetTotalBalance;

public sealed class GetTotalBalanceHandler(
	IUserReadRepository userReadRepository,
	IDateProvider dateProvider
) : IRequestHandler<GetTotalBalanceQuery, TotalBalanceDto>
{
	public async Task<TotalBalanceDto> Handle(
		GetTotalBalanceQuery query,
		CancellationToken ct = default)
	{
		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: query.UserId);

		decimal balance = await userReadRepository.GetTotalBalanceAsync(
			userId: query.UserId,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: dateProvider.UtcNow),
			ct: ct
		);

		return new TotalBalanceDto(Balance: balance, Currency: user.BaseCurrency);
	}
}