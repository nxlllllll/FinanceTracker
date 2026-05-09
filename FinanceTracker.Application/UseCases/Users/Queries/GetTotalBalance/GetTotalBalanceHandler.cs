using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetTotalBalance;

public sealed class GetTotalBalanceHandler(
	IUserReadRepository userReadRepository,
	IAccountReadRepository accountReadRepository,
	ICurrencyConversionService currencyConversionService,
	IDateProvider dateProvider
) : IRequestHandler<GetTotalBalanceQuery, TotalBalanceDto>
{
	public async Task<TotalBalanceDto> Handle(
		GetTotalBalanceQuery query,
		CancellationToken ct = default)
	{
		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: query.UserId);

		IReadOnlyList<AccountDto> accounts = await accountReadRepository.GetAllAsync(
			userId: query.UserId,
			isArchived: false,
			ct: ct
		);
		
		decimal totalBalance = 0;

		foreach (AccountDto account in accounts)
		{
			if (account.Currency == user.BaseCurrency)
			{
				totalBalance += account.Balance;
				continue;
			}

			ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
				fromCurrency: account.Currency,
				toCurrency: user.BaseCurrency,
				date: DateOnly.FromDateTime(dateTime: dateProvider.UtcNow),
				ct: ct
			);

			totalBalance += account.Balance * conversion.Rate;
		}

		return new TotalBalanceDto(Balance: totalBalance, Currency: user.BaseCurrency);
	}
}