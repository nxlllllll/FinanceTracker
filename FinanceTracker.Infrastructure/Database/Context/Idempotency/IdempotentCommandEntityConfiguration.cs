using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Idempotency;

public sealed class IdempotentCommandEntityConfiguration : IEntityTypeConfiguration<IdempotentCommandEntity>
{
	public void Configure(EntityTypeBuilder<IdempotentCommandEntity> builder)
	{
		builder.ToTable(name: "idempotent_commands");

		builder.HasKey(keyExpression: e => new { e.IdempotencyKey, e.CommandType, e.UserId });

		builder.Property(propertyExpression: e => e.IdempotencyKey)
			.HasColumnName(name: "idempotency_key");

		builder.Property(propertyExpression: e => e.CommandType)
			.HasColumnName(name: "command_type")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: e => e.ReservationId)
			.HasColumnName(name: "reservation_id");

		builder.Property(propertyExpression: e => e.ResponseJson)
			.HasColumnName(name: "response_json")
			.HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: e => e.ReservedAt)
			.HasColumnName(name: "reserved_at");

		builder.Property(propertyExpression: e => e.ExpiresAt)
			.HasColumnName(name: "expires_at");
	}
}
