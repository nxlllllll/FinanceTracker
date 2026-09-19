using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Chaos;

public sealed class DatabaseConnectionDropChaosTests : MediatorFixture
{
	private ConnectionDropInterceptor _connectionDrop = null!;

	protected override void ConfigureHostServices(IServiceCollection services, IConfiguration configuration)
	{
		_connectionDrop = new ConnectionDropInterceptor(
			connectionString: configuration.GetConnectionString(name: nameof(FinanceTrackerContext))!,
			commandTextFragment: "INSERT INTO events"
		);

		services.ConfigureDbContext<FinanceTrackerContext>(optionsAction: options => options.AddInterceptors(_connectionDrop));
	}

	[Test]
	public async Task ACommandWhoseConnectionDropsMidWrite_ShouldBeRetriedAndApplyOnce()
	{
		Guid userId = await new UserBuilder(context: Context).CreateAsync();
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");

		_connectionDrop.Arm();

		Result<Guid, AppException> result = default;
		Exception? escaped = null;

		try
		{
			result = await Mediator.Send(request: new CreateAccountCommand(
				UserId: userId,
				Name: Name.Create(value: "Основной счёт").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "RUB").Value,
				InitialBalance: 0m
			)
			{ IdempotencyKey = Guid.CreateVersion7() });
		}
		catch (Exception exception)
		{
			escaped = exception;
		}

		await Assert.That(value: _connectionDrop.Fired).IsTrue().Because(message: """
			Without this the rest of the test is vacuous: the command would have run on a healthy
			connection and proved nothing about a dropped one.
		""");

		await Assert.That(value: escaped).IsNull().Because(message: $"""
			A connection dropped mid-write is exactly what TransientRetryBehaviour exists for: the server
			reports it as retryable, and a second attempt on a fresh connection goes through. Instead
			this escaped to the caller, which the API answers with a 500:

			{escaped}
		""");

		await Assert.That(value: result.IsSuccess).IsTrue().Because(message: $"The retried command came back as {result.Error?.GetType().Name}: {result.Error?.Message}");

		await using FinanceTrackerContext read = CreateReadContext();

		int accountStreams = await read.Events
			.Where(predicate: e => e.AggregateType == AggregateTypeNames.Account)
			.Select(selector: e => e.AggregateId)
			.Distinct()
			.CountAsync();

		await Assert.That(value: accountStreams).IsEqualTo(expected: 1).Because(message: """
			The first attempt's transaction died with its connection, so only the retry may have written.
			Two streams would mean a retry repeated work the first attempt had already committed.
		""");
	}
}
