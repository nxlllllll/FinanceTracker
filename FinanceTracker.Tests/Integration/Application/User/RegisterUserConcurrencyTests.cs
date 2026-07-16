using System.Net;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Application.User;

public sealed class RegisterUserConcurrencyTests : DatabaseFixture
{
	private CurrencyBuilder _currencyBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		_currencyBuilder = new CurrencyBuilder(context: Context);
		await _currencyBuilder.CreateAsync(code: "RUB");
	}

	private RegisterUserHandler BuildHandler(FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context)
	{
		UserReadRepository readRepository = new UserReadRepository(context: context);
		UserWriteRepository writeRepository = new UserWriteRepository(context: context);
		EFUnitOfWork unitOfWork = new EFUnitOfWork(context: context, logger: NullLogger<EFUnitOfWork>.Instance);

		IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
		passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: "hashed_password");

		return new RegisterUserHandler(
			userAuthRepository: readRepository,
			userWriteRepository: writeRepository,
			passwordHasher: passwordHasher,
			unitOfWork: unitOfWork,
			postCommitNotifications: Substitute.For<IPostCommitNotifications>(),
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RegisterUserHandler>.Instance
		);
	}

	private static RegisterUserCommand BuildCommand(string email) => new RegisterUserCommand(
		Email: Email.Create(value: email).Value,
		Password: "Password123!",
		BaseCurrencyCode: Currency.Create(value: "RUB").Value,
		IpAddress: IPAddress.Parse(ipString: "203.0.113.10")
	);

	[Test]
	public async Task Handle_WhenTwoRequestsWithSameEmail_ShouldAllowOnlyOneRegistration()
	{
		string email = $"{Guid.CreateVersion7():N}@test.com";

		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context1 = CreateAdditionalContext();
		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context2 = CreateAdditionalContext();

		RegisterUserHandler handler1 = BuildHandler(context: context1);
		RegisterUserHandler handler2 = BuildHandler(context: context2);

		RegisterUserCommand command = BuildCommand(email: email);

		Result<Guid, AppException>[] results = await Task.WhenAll(
			handler1.Handle(command: command, ct: CancellationToken.None),
			handler2.Handle(command: command, ct: CancellationToken.None)
		);

		int successCount = results.Count(predicate: r => r.IsSuccess);
		int failureCount = results.Count(predicate: r => r.IsFailure);

		await Assert.That(value: successCount).IsEqualTo(expected: 1);
		await Assert.That(value: failureCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_WhenTwoRequestsWithSameEmail_ShouldPersistExactlyOneUser()
	{
		string email = $"{Guid.CreateVersion7():N}@test.com";

		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context1 = CreateAdditionalContext();
		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context2 = CreateAdditionalContext();

		RegisterUserHandler handler1 = BuildHandler(context: context1);
		RegisterUserHandler handler2 = BuildHandler(context: context2);

		RegisterUserCommand command = BuildCommand(email: email);

		await Task.WhenAll(
			handler1.Handle(command: command, ct: CancellationToken.None),
			handler2.Handle(command: command, ct: CancellationToken.None)
		);

		int count = await Context.Users.CountAsync(
			predicate: u => u.Email == Email.Create(value: email).Value
		);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_WhenTwoRequestsWithSameEmail_FailedRequestShouldReturnEmailException()
	{
		string email = $"{Guid.CreateVersion7():N}@test.com";

		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context1 = CreateAdditionalContext();
		await using FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext context2 = CreateAdditionalContext();

		RegisterUserHandler handler1 = BuildHandler(context: context1);
		RegisterUserHandler handler2 = BuildHandler(context: context2);

		RegisterUserCommand command = BuildCommand(email: email);

		Result<Guid, AppException>[] results = await Task.WhenAll(
			handler1.Handle(command: command, ct: CancellationToken.None),
			handler2.Handle(command: command, ct: CancellationToken.None)
		);

		Result<Guid, AppException>? failure = results.FirstOrDefault(predicate: r => r.IsFailure);

		await Assert.That(value: failure).IsNotNull();
		await Assert.That(value: failure.Value.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task Handle_WhenTenConcurrentRequestsWithSameEmail_ShouldPersistExactlyOneUser()
	{
		string email = $"{Guid.CreateVersion7():N}@test.com";
		RegisterUserCommand command = BuildCommand(email: email);

		IEnumerable<Task<Result<Guid, AppException>>> tasks = Enumerable.Range(start: 0, count: 10).Select(selector: _ =>
		{
			FinanceTracker.Infrastructure.Database.Context.FinanceTrackerContext ctx = CreateAdditionalContext();
			RegisterUserHandler handler = BuildHandler(context: ctx);
			return handler.Handle(command: command, ct: CancellationToken.None);
		});

		Result<Guid, AppException>[] results = await Task.WhenAll(tasks);

		int successCount = results.Count(predicate: r => r.IsSuccess);
		int count = await Context.Users.CountAsync(predicate: u => u.Email == Email.Create(value: email).Value);

		await Assert.That(value: successCount).IsEqualTo(expected: 1);
		await Assert.That(value: count).IsEqualTo(expected: 1);
	}
}
