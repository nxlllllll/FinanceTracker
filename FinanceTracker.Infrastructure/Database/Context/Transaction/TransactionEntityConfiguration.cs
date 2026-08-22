using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Transaction;

public sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<TransactionEntity>
{
	public void Configure(EntityTypeBuilder<TransactionEntity> builder)
	{
		builder.ToTable(name: "rm_transactions");

		builder.HasKey(keyExpression: t => t.Id);

		builder.Property(propertyExpression: t => t.Amount).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.Currency).HasColumnName(name: "currency_code");

		builder.Property(propertyExpression: t => t.BaseCurrency).HasColumnName(name: "base_currency_code");

		builder.Property(propertyExpression: t => t.Direction)
			.HasColumnName(name: "direction_type")
			.HasMaxLength(maxLength: 10)
			.HasConversion<SnakeCaseEnumConverter<DirectionType>>();

		builder.Property(propertyExpression: t => t.ExchangeRate).HasColumnType(typeName: "numeric(18,6)");

		builder.Property(propertyExpression: t => t.RateStatus)
			.HasMaxLength(maxLength: 16)
			.HasConversion<SnakeCaseEnumConverter<RateStatus>>();

		builder.Property(propertyExpression: t => t.Description).HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: t => t.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
