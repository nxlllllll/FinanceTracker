using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class CurrencyBuilder(FinanceTrackerContext context)
{
	public async Task<Core.ValueObjects.Currency> CreateAsync(string code = "RUB")
	{
		bool exists = await context.Currencies.AnyAsync(c => c.Code == code);
		if (exists)
			return Core.ValueObjects.Currency.Reconstitute(value: code);

		await context.Currencies.AddAsync(new CurrencyEntity()
		{
			Code = Core.ValueObjects.Currency.Create(value: code).Value,
			Name = code switch
			{
				"RUB" => "Российский рубль",
				"USD" => "Доллар США",
				"EUR" => "Евро",
				_ => code
			},
			Symbol = code switch
			{
				"RUB" => "?",
				"USD" => "$",
				"EUR" => "€",
				_ => code
			},
			IsActive = true
		});
		await context.SaveChangesAsync();
		return Core.ValueObjects.Currency.Reconstitute(value: code);
	}
}
