using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.AccountType;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountType;

public sealed class GetAccountTypeHandler(
	IAccountTypeReadRepository accountTypeReadRepository
) : IRequestHandler<GetAccountTypeQuery, AccountTypeDto?>
{
	public async Task<AccountTypeDto?> Handle(
		GetAccountTypeQuery query,
		CancellationToken ct
	) => await accountTypeReadRepository.GetByTypeAsync(type: query.AccountType, ct: ct);
}