using FinanceTracker.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<RoleEntity>
{
	public void Configure(EntityTypeBuilder<RoleEntity> builder)
	{
		builder.ToTable(name: "roles");

		builder.HasKey(keyExpression: e => e.Id);

		builder.Property(propertyExpression: e => e.SystemKey)
			.HasMaxLength(maxLength: 32)
			.HasConversion(
				convertToProviderExpression: role => role.ToString()!.ToLowerInvariant(),
				convertFromProviderExpression: value => Enum.Parse<SystemRole>(value: value, ignoreCase: true)
			);

		builder.Property(propertyExpression: e => e.DisplayName).HasMaxLength(maxLength: 100);
	}
}
