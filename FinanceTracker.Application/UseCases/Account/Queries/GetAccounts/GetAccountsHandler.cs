using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccounts;

public sealed class GetAccountsHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountsQuery, Result<IReadOnlyList<AccountReadModel>, AppException>>
{
	public async Task<Result<IReadOnlyList<AccountReadModel>, AppException>> Handle(
		GetAccountsQuery query,
		CancellationToken ct = default)
	{
		return Result<IReadOnlyList<AccountReadModel>, AppException>.Success(value: await accountReadRepository.GetAllAsync(
			userId: query.UserId,
			isArchived: query.IsArchived,
			ct: ct
		));
	}
}
