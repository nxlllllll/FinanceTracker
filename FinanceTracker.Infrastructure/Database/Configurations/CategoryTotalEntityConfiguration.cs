using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class CategoryTotalEntityConfiguration : IEntityTypeConfiguration<CategoryTotalEntity>
{
    public void Configure(EntityTypeBuilder<CategoryTotalEntity> builder)
    {
        builder.ToTable(name: "rm_category_totals");
 
        builder.HasKey(keyExpression: c => c.Id);
 
        builder.Property(propertyExpression: c => c.UserId)
            .HasColumnName(name: "user_id");
 
        builder.Property(propertyExpression: c => c.CategoryId)
            .HasColumnName(name: "category_id");
 
        builder.Property(propertyExpression: c => c.Period)
            .HasColumnName(name: "period")
            .HasColumnType(typeName: "date");
 
        builder.Property(propertyExpression: c => c.Total)
            .HasColumnName(name: "total")
            .HasColumnType(typeName: "numeric(18,2)");
 
        builder.Property(propertyExpression: c => c.TransactionCount)
            .HasColumnName(name: "transaction_count");
 
        builder.Property(propertyExpression: c => c.UpdatedAt)
            .HasColumnName(name: "updated_at");
 
        builder.HasIndex(indexExpression: c => new { c.UserId, c.CategoryId, c.Period })
            .IsUnique()
            .HasDatabaseName(name: "uq_rm_category_totals_period");
 
        builder.HasOne<CategoryEntity>().WithMany()
            .HasForeignKey(foreignKeyExpression: c => c.CategoryId)
            .OnDelete(deleteBehavior: DeleteBehavior.Restrict);
 
        builder.HasOne<UserEntity>().WithMany()
            .HasForeignKey(foreignKeyExpression: c => c.UserId)
            .OnDelete(deleteBehavior: DeleteBehavior.Cascade);
    }
}