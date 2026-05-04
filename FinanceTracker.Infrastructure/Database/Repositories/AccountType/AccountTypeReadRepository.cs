using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.AccountType;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.AccountType;

public sealed class AccountTypeReadRepository(
	FinanceTrackerContext context
) : IAccountTypeReadRepository
{
	public async Task<IReadOnlyList<AccountTypeDto>> GetAllAsync(CancellationToken ct = default)
	{
		return await context.AccountTypes.AsNoTracking().Select(selector: accountType => new AccountTypeDto(
			Type: accountType.Type,
			Name: accountType.Name,
			Description: accountType.Description
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<AccountTypeDto?> GetByTypeAsync(string type, CancellationToken ct = default)
	{
		return await context.AccountTypes.AsNoTracking()
			.Where(predicate: accountType => accountType.Type == type)
			.Select(a => new AccountTypeDto(
				Type: a.Type,
				Name: a.Name,
				Description: a.Description
			)).FirstOrDefaultAsync(cancellationToken: ct);	
	}
}