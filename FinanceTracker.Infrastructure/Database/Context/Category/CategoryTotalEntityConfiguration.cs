using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Category;

public sealed class CategoryTotalEntityConfiguration : IEntityTypeConfiguration<CategoryTotalEntity>
{
	public void Configure(EntityTypeBuilder<CategoryTotalEntity> builder)
	{
		builder.ToTable(name: "rm_category_totals");

		builder.HasKey(keyExpression: c => c.Id);

		builder.Property(propertyExpression: c => c.Period).HasColumnType(typeName: "date");

		builder.Property(propertyExpression: c => c.Total).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: c => c.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: c => c.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: c => c.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}
