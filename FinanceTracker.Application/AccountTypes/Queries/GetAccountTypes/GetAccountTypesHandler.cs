using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.AccountType;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountTypes;

public sealed class GetAccountTypesHandler(
	IAccountTypeReadRepository accountTypeReadRepository
) : IRequestHandler<GetAccountTypesQuery, IReadOnlyList<AccountTypeDto>>
{
	public async Task<IReadOnlyList<AccountTypeDto>> Handle(
		GetAccountTypesQuery query,
		CancellationToken ct = default
	) => await accountTypeReadRepository.GetAllAsync(ct: ct);
}