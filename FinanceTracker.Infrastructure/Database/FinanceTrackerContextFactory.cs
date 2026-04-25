using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Infrastructure.Database;

public sealed class FinanceTrackerContextFactory : IDesignTimeDbContextFactory<FinanceTrackerContext>
{
	public FinanceTrackerContext CreateDbContext(string[] args)
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(
			connectionString: "Host=localhost;Database=FinanceTracker;Username=postgres;Password=nullReference@@1743;Pooling=true;MaxPoolSize=100"
		).Options;

		return new FinanceTrackerContext(options: options);
	}
}