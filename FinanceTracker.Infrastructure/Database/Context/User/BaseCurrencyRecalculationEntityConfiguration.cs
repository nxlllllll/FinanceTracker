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

		builder.Property(propertyExpression: e => e.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: e => e.Status)
			.HasColumnName(name: "status")
			.HasMaxLength(maxLength: 16)
			.HasConversion<SnakeCaseEnumConverter<BaseCurrencyRecalculationStatus>>();

		builder.Property(propertyExpression: e => e.TargetCurrency)
			.HasColumnName(name: "target_currency")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: e => e.RequestedAt)
			.HasColumnName(name: "requested_at");

		builder.Property(propertyExpression: e => e.LockedUntil)
			.HasColumnName(name: "locked_until");

		builder.Property(propertyExpression: e => e.Attempts)
			.HasColumnName(name: "attempts");

		builder.Property(propertyExpression: e => e.LastError)
			.HasColumnName(name: "last_error");
	}
}
