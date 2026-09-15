using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace FinanceTracker.Tests.Unit.Helpers;

/// <summary>
/// Once armed, kills the PostgreSQL backend behind the next command whose SQL contains
/// <c>commandTextFragment</c>, so that command fails the way it would on a dropped connection.
/// </summary>
public sealed class ConnectionDropInterceptor(string connectionString, string commandTextFragment) : DbCommandInterceptor
{
	private bool _armed;

	public bool Fired { get; private set; }

	public void Arm() => _armed = true;

	public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result,
		CancellationToken cancellationToken = default)
	{
		await TerminateIfArmedAsync(command: command, ct: cancellationToken);
		return result;
	}

	public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		await TerminateIfArmedAsync(command: command, ct: cancellationToken);
		return result;
	}

	private async Task TerminateIfArmedAsync(DbCommand command, CancellationToken ct)
	{
		if (!_armed || !command.CommandText.Contains(value: commandTextFragment, comparisonType: StringComparison.OrdinalIgnoreCase))
			return;

		_armed = false;

		int processId = ((NpgsqlConnection)command.Connection!).ProcessID;

		string adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString: connectionString)
		{
			Pooling = false
		}.ConnectionString;

		await using NpgsqlConnection admin = new NpgsqlConnection(connectionString: adminConnectionString);
		await admin.OpenAsync(cancellationToken: ct);

		await using NpgsqlCommand terminate = new NpgsqlCommand(cmdText: "SELECT pg_terminate_backend(@pid, 5000)", connection: admin);
		terminate.Parameters.AddWithValue(parameterName: "pid", value: processId);
		await terminate.ExecuteScalarAsync(cancellationToken: ct);

		Fired = true;
	}
}
