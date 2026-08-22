using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Currency;

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

		builder.Property(propertyExpression: r => r.Rate).HasColumnType(typeName: "numeric(18,6)");

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
