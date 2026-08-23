using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetEntityConfiguration : IEntityTypeConfiguration<BudgetEntity>
{
	public void Configure(EntityTypeBuilder<BudgetEntity> builder)
	{
		builder.ToTable(name: "budgets");

		builder.HasKey(keyExpression: b => b.Id);

		builder.Property(propertyExpression: b => b.From)
			.HasColumnName(name: "date_from")
			.HasColumnType(typeName: "date");

		builder.Property(propertyExpression: b => b.To)
			.HasColumnName(name: "date_to")
			.HasColumnType(typeName: "date");

		builder.Property(propertyExpression: b => b.Currency).HasColumnName(name: "currency_code");

		builder.Property(propertyExpression: b => b.Amount).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: b => b.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
