using System.Globalization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Cli.Commands;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Services.Rebuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Cli;

public sealed class Program
{
	private const int DefaultBatchSize = 50;
	private const int DefaultParallelism = 5;

	private static string Usage => $"""
		FinanceTracker administrative commands.

		Usage:
		  grant-root <email>                        Grant the root role to an existing user.

		  rebuild-projection --projection <name> <aggregateId>
		                                            Replay one aggregate's events into its read model.

		  rebuild-projection --projection <name> --all --yes
		      [--batch-size <n>] [--parallelism <n>]
		                                            Replay every aggregate. Overwrites the whole read model.
		                                            Defaults: batch size {DefaultBatchSize}, parallelism {DefaultParallelism}.

		  Projections: {String.Join(separator: ", ", values: ProjectionRegistry.Names)}
		""";

	public static async Task<int> Main(string[] args)
	{
		if (args.Length == 0)
			return Fail();

		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

		builder.Services.AddPersistence(configuration: builder.Configuration);
		builder.Services.AddApplication();
		builder.Services.AddScoped<GrantRootCommand>();
		builder.Services.AddScoped<RebuildProjectionCommand>();

		using IHost host = builder.Build();
		await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

		return args[0] switch
		{
			"grant-root" when args.Length == 2 => await scope.ServiceProvider.GetRequiredService<GrantRootCommand>().ExecuteAsync(email: args[1]),
			"rebuild-projection" => await RunRebuildAsync(scope: scope, args: args),
			_ => Fail()
		};
	}

	private static async Task<int> RunRebuildAsync(AsyncServiceScope scope, string[] args)
	{
		if (!TryReadValue(args: args, option: "--projection", value: out string? projectionName))
			return Fail();

		if (projectionName is null)
		{
			Console.WriteLine(value: "rebuild-projection needs --projection <name>.");
			return Fail();
		}

		RebuildProjectionCommand command = scope.ServiceProvider.GetRequiredService<RebuildProjectionCommand>();

		if (!HasFlag(args: args, flag: "--all"))
		{
			string? aggregateId = args.LastOrDefault(predicate: arg => !arg.StartsWith(value: "--", comparisonType: StringComparison.Ordinal) && arg != projectionName && arg != args[0]);

			return aggregateId is null
				? Fail()
				: await command.ExecuteForAggregateAsync(projectionName: projectionName, aggregateId: aggregateId);
		}

		if (!TryReadInt(args: args, option: "--batch-size", fallback: DefaultBatchSize, value: out int batchSize))
			return Fail();

		if (!TryReadInt(args: args, option: "--parallelism", fallback: DefaultParallelism, value: out int parallelism))
			return Fail();

		return await command.ExecuteForAllAsync(
			projectionName: projectionName,
			confirmed: HasFlag(args: args, flag: "--yes"),
			batchSize: batchSize,
			parallelism: parallelism
		);
	}

	private static bool HasFlag(string[] args, string flag)
		=> args.Contains(value: flag, comparer: StringComparer.Ordinal);

	private static bool TryReadValue(string[] args, string option, out string? value)
	{
		value = null;

		int index = Array.IndexOf(array: args, value: option);

		if (index < 0)
			return true;

		if (index + 1 >= args.Length || args[index + 1].StartsWith(value: "--", comparisonType: StringComparison.Ordinal))
		{
			Console.WriteLine(value: $"{option} needs a value.");
			return false;
		}

		value = args[index + 1];
		return true;
	}

	private static bool TryReadInt(string[] args, string option, int fallback, out int value)
	{
		value = fallback;

		if (!TryReadValue(args: args, option: option, value: out string? raw))
			return false;

		if (raw is null)
			return true;

		if (!Int32.TryParse(s: raw, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out value))
		{
			Console.WriteLine(value: $"'{raw}' is not a valid {option}.");
			return false;
		}

		return true;
	}

	private static int Fail()
	{
		Console.WriteLine(value: Usage);
		return 1;
	}
}
