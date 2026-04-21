using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountTypes;

public sealed class GetAccountTypesHandler(
	IAccountTypeRepository accountTypeRepository
) : IRequestHandler<GetAccountTypesQuery, IReadOnlyList<AccountTypeDto>>
{
	public async Task<IReadOnlyList<AccountTypeDto>> Handle(
		GetAccountTypesQuery query,
		CancellationToken ct = default
	) => await accountTypeRepository.GetAllAsync(ct: ct);
}