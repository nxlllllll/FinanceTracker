using FinanceTracker.Core.Domains.Operation;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class OperationEntityConfiguration : IEntityTypeConfiguration<OperationEntity>
{
	public void Configure(EntityTypeBuilder<OperationEntity> builder)
	{
		builder.ToTable(name: "rm_operations");

		builder.HasKey(keyExpression: o => o.Id);

		builder.Property(propertyExpression: o => o.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: o => o.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(o => o.Type)
		    .HasColumnName(name: "type")
		    .HasMaxLength(maxLength: 20)
		    .HasConversion(
		        convertToProviderExpression: operation => operation.ToString(),
		        convertFromProviderExpression: operation => Enum.Parse<OperationType>(value: operation)
		    );

		builder.Property(propertyExpression: o => o.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.Property(propertyExpression: o => o.Description)
			.HasColumnName(name: "description");

		builder.Property(propertyExpression: o => o.Payload)
			.HasColumnName(name: "payload")
			.HasColumnType(typeName: "jsonb");
	}
}