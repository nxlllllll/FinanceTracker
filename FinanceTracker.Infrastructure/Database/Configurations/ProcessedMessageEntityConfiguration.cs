using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class ProcessedMessageEntityConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
	public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
	{
		builder.ToTable(name: "processed_messages");

		builder.HasKey(keyExpression: m => m.MessageId);

		builder.Property(propertyExpression: m => m.MessageId)
			.HasColumnName(name: "message_id");

		builder.Property(propertyExpression: m => m.ProcessedAt)
			.HasColumnName(name: "processed_at");
	}
}