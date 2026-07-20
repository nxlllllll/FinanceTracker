using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
	public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
	{
		builder.ToTable(name: "user_roles");

		builder.HasKey(keyExpression: e => new { e.UserId, e.RoleId });

		builder.Property(propertyExpression: e => e.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: e => e.RoleId)
			.HasColumnName(name: "role_id");

		builder.Property(propertyExpression: e => e.AssignedAt)
			.HasColumnName(name: "assigned_at");
	}
}
