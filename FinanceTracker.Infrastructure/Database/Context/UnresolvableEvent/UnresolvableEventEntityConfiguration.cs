using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;

public sealed class UnresolvableEventEntityConfiguration : IEntityTypeConfiguration<UnresolvableEventEntity>
{
	public void Configure(EntityTypeBuilder<UnresolvableEventEntity> builder)
	{
		builder.ToTable(name: "unresolvable_events");

		builder.HasKey(keyExpression: e => e.Id);

		builder.Property(propertyExpression: e => e.Type)
			.HasColumnName(name: "type_code")
			.HasMaxLength(maxLength: 50)
			.HasConversion(converter: new SnakeCaseEnumConverter<UnresolvableEventType>());

		builder.Property(propertyExpression: e => e.Payload).HasColumnType(typeName: "jsonb");
	}
}
