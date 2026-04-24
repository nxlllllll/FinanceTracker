using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class CurrencyBuilder(FinanceTrackerContext context)
{
	public async Task<string> CreateAsync(string code = "RUB")
	{
		bool exists = await context.Currencies.AnyAsync(c => c.Code == code);
		if (exists)
			return code;

		await context.Currencies.AddAsync(new CurrencyEntity()
		{
			Code = code,
			Name = code switch
			{
				"RUB" => "Российский рубль",
				"USD" => "Доллар США",
				"EUR" => "Евро",
				_ => code
			},
			Symbol = code switch
			{
				"RUB" => "₽",
				"USD" => "$",
				"EUR" => "€",
				_ => code
			},
			IsActive = true
		});
		await context.SaveChangesAsync();
		return code;
	}
}