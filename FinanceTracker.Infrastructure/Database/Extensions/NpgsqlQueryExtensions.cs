using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Extensions;

/// <summary>
/// Creates a short-lived, independent NpgsqlConnection for raw read queries.
/// Using a dedicated connection (rather than reusing the EF context connection)
/// avoids interfering with EF's own connection lifecycle and allows the reader
/// to stay open while other EF queries run in the same scope.
/// </summary>
internal static class NpgsqlQueryExtensions
{
	public static async ValueTask<NpgsqlConnection> OpenReadConnectionAsync(
		this DbContext context,
		CancellationToken ct = default)
	{
		string connectionString = context.Database.GetConnectionString()
			?? throw new InvalidOperationException(message: "Database connection string is not configured.");

		NpgsqlConnection conn = new NpgsqlConnection(connectionString: connectionString);
		await conn.OpenAsync(cancellationToken: ct);
		return conn;
	}
}