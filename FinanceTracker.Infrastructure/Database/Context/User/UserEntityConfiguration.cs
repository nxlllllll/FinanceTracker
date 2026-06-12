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

		builder.Property(propertyExpression: a => a.Id)
			.HasColumnName(name: "id");
		
		builder.Property(propertyExpression: u => u.Email)
			.HasColumnName(name: "email")
			.HasMaxLength(maxLength: 255)
			.HasConversion(
				convertToProviderExpression: email => email.Value,
				convertFromProviderExpression: email => Email.Reconstitute(value: email)
			);

		builder.Property(propertyExpression: u => u.PasswordHash)
			.HasColumnName(name: "password_hash")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: u => u.BaseCurrencyCode)
			.HasColumnName(name: "base_currency_code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: u => u.RowVersion)
			.HasColumnName(name: "row_version")
			.HasDefaultValue(value: 0)
			.IsConcurrencyToken();

		builder.Property(propertyExpression: u => u.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasIndex(indexExpression: u => u.Email).IsUnique();

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.BaseCurrencyCode)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}