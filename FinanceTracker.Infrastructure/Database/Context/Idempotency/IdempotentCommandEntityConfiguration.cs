using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Idempotency;

public sealed class IdempotentCommandEntityConfiguration : IEntityTypeConfiguration<IdempotentCommandEntity>
{
	public void Configure(EntityTypeBuilder<IdempotentCommandEntity> builder)
	{
		builder.ToTable(name: "idempotent_commands");

		builder.HasKey(keyExpression: e => new { e.IdempotencyKey, e.CommandType, e.UserId });

		builder.Property(propertyExpression: e => e.CommandType).HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.ResponseJson).HasColumnType(typeName: "jsonb");
	}
}
