using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserSessionEntityConfiguration : IEntityTypeConfiguration<UserSessionEntity>
{
	public void Configure(EntityTypeBuilder<UserSessionEntity> builder)
	{
		builder.ToTable(name: "user_sessions");

		builder.HasKey(keyExpression: s => s.Id);

		builder.Property(propertyExpression: s => s.RefreshTokenHash).HasMaxLength(maxLength: 64);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: s => s.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}
