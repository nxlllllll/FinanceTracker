using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
	public void Configure(EntityTypeBuilder<UserEntity> builder)
	{
		builder.ToTable(name: "users");

		builder.HasKey(keyExpression: u => u.Id);

		builder.Property(propertyExpression: u => u.Email)
			.HasMaxLength(maxLength: 255)
			.HasConversion(
				convertToProviderExpression: email => email.Value,
				convertFromProviderExpression: email => Email.Reconstitute(value: email)
			);

		builder.Property(propertyExpression: u => u.PasswordHash).HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: u => u.TimeZoneId)
			.HasMaxLength(maxLength: 64)
			.HasConversion(
				convertToProviderExpression: timeZone => timeZone.Value,
				convertFromProviderExpression: timeZone => TimeZoneId.Reconstitute(value: timeZone)
			);

		builder.Property(propertyExpression: u => u.RowVersion).HasDefaultValue(value: 0);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.BaseCurrencyCode)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
