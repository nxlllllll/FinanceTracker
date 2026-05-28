using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetAccounts;

public sealed class GetAccountsHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountsQuery, IReadOnlyList<AccountReadModel>>
{
	public async Task<IReadOnlyList<AccountReadModel>> Handle(
		GetAccountsQuery query,
		CancellationToken ct = default
	) => await accountReadRepository.GetAllAsync(userId: query.UserId, isArchived: query.IsArchived, ct: ct);
}