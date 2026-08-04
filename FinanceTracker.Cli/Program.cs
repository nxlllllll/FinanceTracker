using FinanceTracker.Application.Configurations;
using FinanceTracker.Cli.Commands;
using FinanceTracker.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Cli;

public sealed class Program
{
	private const string Usage = """
		FinanceTracker administrative commands.

		Usage:
		  grant-root <email>    Grant the root role to an existing user.
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

		using IHost host = builder.Build();
		await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

		return args[0] switch
		{
			"grant-root" when args.Length == 2 => await scope.ServiceProvider.GetRequiredService<GrantRootCommand>().ExecuteAsync(email: args[1]),
			_ => Fail()
		};
	}

	private static int Fail()
	{
		Console.WriteLine(value: Usage);
		return 1;
	}
}
