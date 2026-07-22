using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
	public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
	{
		builder.ToTable(name: "role_permissions");

		builder.HasKey(keyExpression: e => new { e.RoleId, e.Permission });

		builder.Property(propertyExpression: e => e.RoleId)
			.HasColumnName(name: "role_id");

		builder.Property(propertyExpression: e => e.Permission)
			.HasColumnName(name: "permission").HasMaxLength(maxLength: 64);

		builder.HasOne<RoleEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: e => e.RoleId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}
