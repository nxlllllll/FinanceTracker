using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetProgressEntityConfiguration : IEntityTypeConfiguration<BudgetProgressEntity>
{
	public void Configure(EntityTypeBuilder<BudgetProgressEntity> builder)
	{
		builder.ToTable(name: "rm_budget_progress");

		builder.HasKey(keyExpression: b => b.BudgetId);

		builder.Property(propertyExpression: b => b.Spent).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: b => b.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<BudgetEntity>().WithOne()
			.HasForeignKey<BudgetProgressEntity>(foreignKeyExpression: b => b.BudgetId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}
