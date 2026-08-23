using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.UserPermission;

public sealed class UserPermissionEntityConfiguration : IEntityTypeConfiguration<UserPermissionEntity>
{
	public void Configure(EntityTypeBuilder<UserPermissionEntity> builder)
	{
		builder.ToTable(name: "user_permissions");

		builder.HasKey(keyExpression: e => new { e.UserId, e.Permission });

		builder.Property(propertyExpression: e => e.Permission).HasMaxLength(maxLength: 64);
	}
}
