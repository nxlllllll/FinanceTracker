using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class BaseCurrencyRecalculationEntityConfiguration : IEntityTypeConfiguration<BaseCurrencyRecalculationEntity>
{
	public void Configure(EntityTypeBuilder<BaseCurrencyRecalculationEntity> builder)
	{
		builder.ToTable(name: "user_base_currency_recalculations");

		builder.HasKey(keyExpression: e => e.UserId);

		builder.Property(propertyExpression: e => e.Status)
			.HasMaxLength(maxLength: 16)
			.HasConversion<SnakeCaseEnumConverter<BaseCurrencyRecalculationStatus>>();

		builder.Property(propertyExpression: e => e.TargetCurrency).HasMaxLength(maxLength: 3);
	}
}
