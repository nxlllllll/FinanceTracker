using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class AuthorizedHandlerAdapterTests
{
	public sealed record TestEntityWithVersion(int Version) : IHasVersion;

	public sealed record TestEntityWithoutVersion;

	public sealed record TestCommandWithExpectedVersion(
		Guid UserId,
		int? ExpectedVersion
	) : IRequest<Result<Guid, AppException>>, IAuthorizable, IHasExpectedVersion;

	public sealed record TestCommandWithoutExpectedVersion(Guid UserId) : IRequest<Result<Guid, AppException>>, IAuthorizable;

	private static AuthorizedHandlerAdapter<TRequest, TEntity, Guid, AppException> BuildAdapter<TRequest, TEntity>(
		IEntityLoader<TRequest, TEntity, AppException> loader,
		IAuthorizedHandler<TRequest, TEntity, Guid, AppException> handler
	) where TRequest : IRequest<Result<Guid, AppException>>, IAuthorizable
	{
		return new AuthorizedHandlerAdapter<TRequest, TEntity, Guid, AppException>(
			loader: loader,
			handler: handler,
			logger: Substitute.For<ILogger<AuthorizedHandlerAdapter<TRequest, TEntity, Guid, AppException>>>()
		);
	}

	[Test]
	public async Task Handle_WhenLoaderFails_ShouldNotCallHandlerAndShouldPropagateTheError()
	{
		var loader = Substitute.For<IEntityLoader<TestCommandWithoutExpectedVersion, TestEntityWithVersion, AppException>>();
		NotFoundException notFound = new NotFoundException(message: "not found", id: Guid.Empty);
		loader.LoadAsync(
			request: Arg.Any<TestCommandWithoutExpectedVersion>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TestEntityWithVersion, AppException>.Failure(error: notFound));

		var handler = Substitute.For<IAuthorizedHandler<TestCommandWithoutExpectedVersion, TestEntityWithVersion, Guid, AppException>>();

		var adapter = BuildAdapter(loader: loader, handler: handler);

		Result<Guid, AppException> result = await adapter.Handle(
			request: new TestCommandWithoutExpectedVersion(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsSameReferenceAs(expected: notFound);
		await handler.DidNotReceive().HandleAsync(
			request: Arg.Any<TestCommandWithoutExpectedVersion>(),
			user: Arg.Any<TestEntityWithVersion>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCommandHasNoExpectedVersion_ShouldSkipTheCheckAndCallHandler()
	{
		var loader = Substitute.For<IEntityLoader<TestCommandWithoutExpectedVersion, TestEntityWithVersion, AppException>>();
		TestEntityWithVersion entity = new TestEntityWithVersion(Version: 5);
		loader.LoadAsync(
			request: Arg.Any<TestCommandWithoutExpectedVersion>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TestEntityWithVersion, AppException>.Success(value: entity));

		var handler = Substitute.For<IAuthorizedHandler<TestCommandWithoutExpectedVersion, TestEntityWithVersion, Guid, AppException>>();
		Guid expectedId = Guid.CreateVersion7();
		handler.HandleAsync(
			request: Arg.Any<TestCommandWithoutExpectedVersion>(),
			user: entity,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, AppException>.Success(value: expectedId));

		var adapter = BuildAdapter(loader: loader, handler: handler);

		Result<Guid, AppException> result = await adapter.Handle(
			request: new TestCommandWithoutExpectedVersion(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: expectedId);
	}

	[Test]
	public async Task Handle_WhenExpectedVersionMatchesActual_ShouldCallHandler()
	{
		var loader = Substitute.For<IEntityLoader<TestCommandWithExpectedVersion, TestEntityWithVersion, AppException>>();
		TestEntityWithVersion entity = new TestEntityWithVersion(Version: 5);
		loader.LoadAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TestEntityWithVersion, AppException>.Success(value: entity));

		var handler = Substitute.For<IAuthorizedHandler<TestCommandWithExpectedVersion, TestEntityWithVersion, Guid, AppException>>();
		handler.HandleAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			user: entity,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, AppException>.Success(value: Guid.CreateVersion7()));

		var adapter = BuildAdapter(loader: loader, handler: handler);

		Result<Guid, AppException> result = await adapter.Handle(
			request: new TestCommandWithExpectedVersion(UserId: Guid.CreateVersion7(), ExpectedVersion: 5),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await handler.Received(requiredNumberOfCalls: 1).HandleAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			user: entity,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenExpectedVersionDoesNotMatchActual_ShouldReturnPreconditionFailedAndNotCallHandler()
	{
		var loader = Substitute.For<IEntityLoader<TestCommandWithExpectedVersion, TestEntityWithVersion, AppException>>();
		TestEntityWithVersion entity = new TestEntityWithVersion(Version: 7);
		loader.LoadAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TestEntityWithVersion, AppException>.Success(value: entity));

		var handler = Substitute.For<IAuthorizedHandler<TestCommandWithExpectedVersion, TestEntityWithVersion, Guid, AppException>>();

		var adapter = BuildAdapter(loader: loader, handler: handler);

		Result<Guid, AppException> result = await adapter.Handle(
			request: new TestCommandWithExpectedVersion(UserId: Guid.CreateVersion7(), ExpectedVersion: 5),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PreconditionFailedException>();

		PreconditionFailedException error = (PreconditionFailedException)result.Error!;
		await Assert.That(value: error.ExpectedVersion).IsEqualTo(expected: 5);
		await Assert.That(value: error.ActualVersion).IsEqualTo(expected: 7);

		await handler.DidNotReceive().HandleAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			user: Arg.Any<TestEntityWithVersion>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenEntityDoesNotImplementIHasVersion_ShouldSkipTheCheckEvenIfCommandHasExpectedVersion()
	{
		var loader = Substitute.For<IEntityLoader<TestCommandWithExpectedVersion, TestEntityWithoutVersion, AppException>>();
		TestEntityWithoutVersion entity = new TestEntityWithoutVersion();
		loader.LoadAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TestEntityWithoutVersion, AppException>.Success(value: entity));

		var handler = Substitute.For<IAuthorizedHandler<TestCommandWithExpectedVersion, TestEntityWithoutVersion, Guid, AppException>>();
		handler.HandleAsync(
			request: Arg.Any<TestCommandWithExpectedVersion>(),
			user: entity,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, AppException>.Success(value: Guid.CreateVersion7()));

		var adapter = BuildAdapter(loader: loader, handler: handler);

		Result<Guid, AppException> result = await adapter.Handle(
			request: new TestCommandWithExpectedVersion(UserId: Guid.CreateVersion7(), ExpectedVersion: 999),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
