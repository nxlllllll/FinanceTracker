using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Currency;

public sealed class CurrencyEntityConfiguration : IEntityTypeConfiguration<CurrencyEntity>
{
	public void Configure(EntityTypeBuilder<CurrencyEntity> builder)
	{
		builder.ToTable(name: "currencies");

		builder.HasKey(keyExpression: c => c.Code);

		builder.Property(propertyExpression: c => c.Code)
			.HasColumnName(name: "code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: c => c.Name)
			.HasColumnName(name: "name")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: c => c.Symbol)
			.HasColumnName(name: "symbol")
			.HasMaxLength(maxLength: 5);

		builder.Property(propertyExpression: c => c.IsActive)
			.HasColumnName(name: "is_active");
	}
}
