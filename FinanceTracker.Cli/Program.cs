using System.Globalization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Cli.Commands;
using FinanceTracker.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Cli;

public sealed class Program
{
	private const int DefaultBatchSize = 50;

	private const string Usage = """
		FinanceTracker administrative commands.

		Usage:
		  grant-root <email>                        Grant the root role to an existing user.

		  rebuild-projection <accountId>            Replay one account's events into the read model.
		  rebuild-projection --all --yes            Replay every account. Overwrites the whole account read model.
		      [--batch-size <n>]                    Accounts per batch. Default: 50.
		""";

	public static async Task<int> Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine(value: Usage);
			return 1;
		}

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
		RebuildProjectionCommand command = scope.ServiceProvider.GetRequiredService<RebuildProjectionCommand>();

		bool all = HasFlag(args: args, flag: "--all");

		if (!all)
		{
			return args.Length == 2 && !args[1].StartsWith(value: "--", comparisonType: StringComparison.Ordinal)
				? await command.ExecuteForAccountAsync(accountId: args[1])
				: Fail();
		}

		if (!TryReadBatchSize(args: args, batchSize: out int batchSize))
			return Fail();

		return await command.ExecuteForAllAsync(
			confirmed: HasFlag(args: args, flag: "--yes"),
			batchSize: batchSize
		);
	}

	private static bool HasFlag(string[] args, string flag)
		=> args.Contains(value: flag, comparer: StringComparer.Ordinal);

	private static bool TryReadBatchSize(string[] args, out int batchSize)
	{
		batchSize = DefaultBatchSize;

		int index = Array.IndexOf(array: args, value: "--batch-size");

		if (index < 0)
			return true;

		if (index + 1 >= args.Length)
		{
			Console.WriteLine(value: "--batch-size needs a value.");
			return false;
		}

		if (!Int32.TryParse(s: args[index + 1], style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out batchSize))
		{
			Console.WriteLine(value: $"'{args[index + 1]}' is not a valid --batch-size.");
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
