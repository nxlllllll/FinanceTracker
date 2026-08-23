using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Category;

public sealed class CategoryEntityConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
	public void Configure(EntityTypeBuilder<CategoryEntity> builder)
	{
		builder.ToTable(name: "categories");

		builder.HasKey(keyExpression: c => c.Id);

		builder.Property(propertyExpression: c => c.Name)
			.HasMaxLength(maxLength: 100)
			.HasConversion(
				convertToProviderExpression: name => name.Value,
				convertFromProviderExpression: name => Name.Reconstitute(value: name)
			);

		builder.Property(propertyExpression: c => c.Type)
			.HasColumnName(name: "type_code")
			.HasMaxLength(maxLength: 10)
			.HasConversion(
				convertToProviderExpression: type => type.ToString().ToLowerInvariant(),
				convertFromProviderExpression: value => Enum.Parse<CategoryType>(value: value, ignoreCase: true)
			);

		builder.Property(propertyExpression: c => c.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<CategoryEntity>()
			.WithMany()
			.HasForeignKey(foreignKeyExpression: c => c.ParentId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.IsRequired(required: false);
	}
}
