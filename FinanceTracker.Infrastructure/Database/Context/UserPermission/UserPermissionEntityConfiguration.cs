using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.UserPermission;

public sealed class UserPermissionEntityConfiguration : IEntityTypeConfiguration<UserPermissionEntity>
{
	public void Configure(EntityTypeBuilder<UserPermissionEntity> builder)
	{
		builder.ToTable(name: "user_permissions");

		builder.HasKey(keyExpression: e => new { e.UserId, e.Permission });

		builder.Property(propertyExpression: e => e.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: e => e.Permission)
			.HasColumnName(name: "permission")
			.HasMaxLength(maxLength: 64);

		builder.Property(propertyExpression: e => e.GrantedAt)
			.HasColumnName(name: "granted_at");
	}
}
