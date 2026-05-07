using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class CurrencyRateEntityConfiguration : IEntityTypeConfiguration<CurrencyRateEntity>
{
	public void Configure(EntityTypeBuilder<CurrencyRateEntity> builder)
	{
		builder.ToTable(name: "currency_rates");

		builder.HasKey(keyExpression: r => new
		{
			r.BaseCode,
			r.TargetCode,
			r.ActualAt
		});

		builder.Property(propertyExpression: r => r.BaseCode)
			.HasColumnName(name: "base_code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: r => r.TargetCode)
			.HasColumnName(name: "target_code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Currency.Reconstitute(value: currency)
			);
		
		builder.Property(propertyExpression: r => r.Rate)
			.HasColumnName(name: "rate")
			.HasColumnType(typeName: "numeric(18,6)");

		builder.Property(propertyExpression: r => r.ActualAt)
			.HasColumnName(name: "actual_at");

		builder.Property(propertyExpression: r => r.CreatedAt)
			.HasColumnName(name: "created_at");
		
		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.BaseCode)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
		
		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.TargetCode)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}