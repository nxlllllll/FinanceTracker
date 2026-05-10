using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetIncomeExpenseSummary;

public sealed class GetIncomeExpenseSummaryHandler(
    IUserReadRepository userReadRepository
) : IRequestHandler<GetIncomeExpenseSummaryQuery, IncomeExpenseSummaryDto>
{
    public async Task<IncomeExpenseSummaryDto> Handle(
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

        return new IncomeExpenseSummaryDto(
            Income: income,
            Expense: expense,
            Currency: user.BaseCurrency,
            Period: query.Period
        );
    }
}