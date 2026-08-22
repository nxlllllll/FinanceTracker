using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
	public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
	{
		builder.ToTable(name: "user_roles");

		builder.HasKey(keyExpression: e => new { e.UserId, e.RoleId });

		builder.HasOne<RoleEntity>().WithMany().HasForeignKey(foreignKeyExpression: e => e.RoleId);
	}
}
