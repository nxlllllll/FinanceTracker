using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserSessionEntityConfiguration : IEntityTypeConfiguration<UserSessionEntity>
{
	public void Configure(EntityTypeBuilder<UserSessionEntity> builder)
	{
		builder.ToTable(name: "user_sessions");

		builder.HasKey(keyExpression: s => s.Id);

		builder.Property(propertyExpression: s => s.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: s => s.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: s => s.RefreshTokenHash)
			.HasColumnName(name: "refresh_token_hash")
			.HasMaxLength(maxLength: 64);

		builder.Property(propertyExpression: s => s.ExpiresAt)
			.HasColumnName(name: "expires_at");

		builder.Property(propertyExpression: s => s.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.Property(propertyExpression: s => s.RevokedAt)
			.HasColumnName(name: "revoked_at");

		builder.HasIndex(indexExpression: s => s.RefreshTokenHash)
			.IsUnique()
			.HasDatabaseName(name: "uq_user_sessions_token_hash");

		builder.HasIndex(indexExpression: s => s.UserId)
			.HasDatabaseName(name: "idx_user_sessions_user");

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: s => s.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);
	}
}
