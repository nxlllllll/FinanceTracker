using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountType;

public sealed class GetAccountTypeHandler(
	IAccountTypeRepository accountTypeRepository
) : IRequestHandler<GetAccountTypeQuery, AccountTypeDto?>
{
	public async Task<AccountTypeDto?> Handle(
		GetAccountTypeQuery query,
		CancellationToken ct
	) => await accountTypeRepository.GetByTypeAsync(type: query.AccountType, ct: ct);
}