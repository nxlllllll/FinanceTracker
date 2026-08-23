using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;

public sealed class RecurringTransactionEntityConfiguration
	: IEntityTypeConfiguration<RecurringTransactionEntity>
{
	public void Configure(EntityTypeBuilder<RecurringTransactionEntity> builder)
	{
		builder.ToTable(name: "recurring_transactions");

		builder.HasKey(keyExpression: r => r.Id);

		builder.Property(propertyExpression: r => r.Amount).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: r => r.Currency).HasColumnName(name: "currency_code");

		builder.Property(propertyExpression: r => r.Direction)
			.HasColumnName(name: "direction_type")
			.HasConversion(
				convertToProviderExpression: v => v.ToString().ToLowerInvariant(),
				convertFromProviderExpression: v => Enum.Parse<DirectionType>(value: v, ignoreCase: true)
			)
			.HasMaxLength(maxLength: 10);

		builder.Property(propertyExpression: r => r.Description).HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: r => r.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: r => r.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: r => r.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
