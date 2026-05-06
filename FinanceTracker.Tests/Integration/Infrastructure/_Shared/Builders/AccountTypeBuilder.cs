using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class AccountTypeBuilder(FinanceTrackerContext context)
{
	public async Task<Core.Domains.Account.AccountType> CreateAsync(
		Core.Domains.Account.AccountType type = Core.Domains.Account.AccountType.Checking)
	{
		string typeCode = type.ToString().ToLower();

		bool exists = await context.AccountTypes.AnyAsync(a => a.Type == typeCode);
		if (exists)
			return type;

		await context.AccountTypes.AddAsync(new AccountTypeEntity()
		{
			Type = typeCode,
			Name = type switch
			{
				Core.Domains.Account.AccountType.Checking => "Текущий счёт",
				Core.Domains.Account.AccountType.Savings => "Сберегательный счёт",
				Core.Domains.Account.AccountType.Cash => "Наличные",
				_ => typeCode
			},
			Description = null
		});
		await context.SaveChangesAsync();
		return type;
	}
}