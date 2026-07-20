using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<RoleEntity>
{
	public void Configure(EntityTypeBuilder<RoleEntity> builder)
	{
		builder.ToTable(name: "roles");

		builder.HasKey(keyExpression: e => e.Id);

		builder.Property(propertyExpression: e => e.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: e => e.SystemKey)
			.HasColumnName(name: "system_key")
			.HasMaxLength(maxLength: 32);

		builder.Property(propertyExpression: e => e.DisplayName)
			.HasColumnName(name: "display_name")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.CreatedAt)
			.HasColumnName(name: "created_at");
	}
}
