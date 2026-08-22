using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Currency;

public sealed class CurrencyEntityConfiguration : IEntityTypeConfiguration<CurrencyEntity>
{
	public void Configure(EntityTypeBuilder<CurrencyEntity> builder)
	{
		builder.ToTable(name: "currencies");

		builder.HasKey(keyExpression: c => c.Code);

		builder.Property(propertyExpression: c => c.Name).HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: c => c.Symbol).HasMaxLength(maxLength: 5);
	}
}
