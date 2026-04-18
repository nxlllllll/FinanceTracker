using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database;

public sealed class FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options) 
	: DbContext(options)
{
	public DbSet<EventEntity> Events => Set<EventEntity>();
	public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
	public DbSet<AccountBalanceEntity> AccountBalances => Set<AccountBalanceEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerContext).Assembly);
}