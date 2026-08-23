using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

public class CurrencyBuilder(FinanceTrackerContext context)
{
	public async Task<Currency> CreateAsync(string code = "RUB", bool isActive = true)
	{
		bool exists = await context.Currencies.AnyAsync(c => c.Code == Currency.Reconstitute(value: code));
		if (exists)
		{
			CurrencyEntity? existing = await context.Currencies.FirstOrDefaultAsync(predicate: c => c.Code == Currency.Reconstitute(value: code));
			if (existing is not null && existing.IsActive != isActive)
			{
				existing.IsActive = isActive;
				await context.SaveChangesAsync();
			}
			return Currency.Reconstitute(value: code);
		}

		await context.Currencies.AddAsync(new CurrencyEntity()
		{
			Code = Currency.Create(value: code).Value,
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
			IsActive = isActive
		});
		await context.SaveChangesAsync();
		return Currency.Reconstitute(value: code);
	}
}
