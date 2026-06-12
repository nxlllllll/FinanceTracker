using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetProgressEntityConfiguration : IEntityTypeConfiguration<BudgetProgressEntity>
{
	public void Configure(EntityTypeBuilder<BudgetProgressEntity> builder)
	{
		builder.ToTable(name: "rm_budget_progress");

		builder.HasKey(keyExpression: b => b.BudgetId);

		builder.Property(propertyExpression: b => b.BudgetId)
			.HasColumnName(name: "budget_id");

		builder.Property(propertyExpression: b => b.Spent)
			.HasColumnName(name: "spent")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: b => b.RowVersion)
			.HasColumnName(name: "row_version")
			.HasDefaultValue(value: 0);

		builder.Property(propertyExpression: b => b.UpdatedAt)
			.HasColumnName(name: "updated_at");

		builder.HasOne<BudgetEntity>().WithOne()
			.HasForeignKey<BudgetProgressEntity>(foreignKeyExpression: b => b.BudgetId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}