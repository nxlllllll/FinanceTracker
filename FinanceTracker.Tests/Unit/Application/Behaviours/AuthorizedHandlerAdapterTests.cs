using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class AuthorizedHandlerAdapterTests
{
	public sealed record PlainRequest(Guid UserId) : IRequest<Result<string, AppException>>, IAuthorizable;

	public sealed record VersionedRequest(Guid UserId, int? ExpectedVersion)
		: IRequest<Result<string, AppException>>, IAuthorizable, IHasExpectedVersion;

	public sealed record NarrowErrorRequest(Guid UserId, int? ExpectedVersion)
		: IRequest<Result<string, ValidationException>>, IAuthorizable, IHasExpectedVersion;

	public sealed class VersionedEntity : IHasVersion, IHasId
	{
		public Guid Id { get; init; } = Guid.CreateVersion7();
		public int Version { get; init; }
	}

	public sealed class UnversionedEntity;

	private static readonly Guid UserId = Guid.CreateVersion7();

	private static AuthorizedHandlerAdapter<VersionedRequest, VersionedEntity, string, AppException> BuildAdapter(
		IEntityLoader<VersionedRequest, VersionedEntity, AppException> loader,
		IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException> handler
	) => new AuthorizedHandlerAdapter<VersionedRequest, VersionedEntity, string, AppException>(
		loader: loader,
		handler: handler,
		logger: NullLogger<AuthorizedHandlerAdapter<VersionedRequest, VersionedEntity, string, AppException>>.Instance
	);

	[Test]
	public async Task ARefusedLoadNeverReachesTheHandler()
	{
		IEntityLoader<VersionedRequest, VersionedEntity, AppException> loader =
			Substitute.For<IEntityLoader<VersionedRequest, VersionedEntity, AppException>>();
		IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException> handler =
			Substitute.For<IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException>>();

		NotFoundException denial = new NotFoundException(message: "not yours", id: Guid.CreateVersion7());
		loader.LoadAsync(request: Arg.Any<VersionedRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<VersionedEntity, AppException>.Failure(error: denial));

		Result<string, AppException> result = await BuildAdapter(loader: loader, handler: handler).Handle(
			request: new VersionedRequest(UserId: UserId, ExpectedVersion: null),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsSameReferenceAs(expected: denial)
			.Because(message: "the loader's reason must survive intact — it is what distinguishes 'missing' from 'not yours'");

		await Assert.That(value: handler.ReceivedCalls()).IsEmpty();
	}

	[Test]
	public async Task ALoadedEntityWithNoVersionDemandGoesStraightToTheHandler()
	{
		VersionedEntity entity = new VersionedEntity { Version = 7 };

		IEntityLoader<VersionedRequest, VersionedEntity, AppException> loader =
			Substitute.For<IEntityLoader<VersionedRequest, VersionedEntity, AppException>>();
		IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException> handler =
			Substitute.For<IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<VersionedRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<VersionedEntity, AppException>.Success(value: entity));
		handler.HandleAsync(request: Arg.Any<VersionedRequest>(), entity: Arg.Any<VersionedEntity>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<string, AppException>.Success(value: "done"));

		Result<string, AppException> result = await BuildAdapter(loader: loader, handler: handler).Handle(
			request: new VersionedRequest(UserId: UserId, ExpectedVersion: null),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();

		await handler.Received(requiredNumberOfCalls: 1).HandleAsync(
			request: Arg.Any<VersionedRequest>(),
			entity: entity,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task AMatchingVersionIsAllowedThrough()
	{
		VersionedEntity entity = new VersionedEntity { Version = 4 };

		IEntityLoader<VersionedRequest, VersionedEntity, AppException> loader =
			Substitute.For<IEntityLoader<VersionedRequest, VersionedEntity, AppException>>();
		IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException> handler =
			Substitute.For<IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<VersionedRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<VersionedEntity, AppException>.Success(value: entity));
		handler.HandleAsync(request: Arg.Any<VersionedRequest>(), entity: Arg.Any<VersionedEntity>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<string, AppException>.Success(value: "done"));

		Result<string, AppException> result = await BuildAdapter(loader: loader, handler: handler).Handle(
			request: new VersionedRequest(UserId: UserId, ExpectedVersion: 4),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task AStaleVersionStopsBeforeTheHandler()
	{
		VersionedEntity entity = new VersionedEntity { Version = 9 };

		IEntityLoader<VersionedRequest, VersionedEntity, AppException> loader =
			Substitute.For<IEntityLoader<VersionedRequest, VersionedEntity, AppException>>();
		IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException> handler =
			Substitute.For<IAuthorizedHandler<VersionedRequest, VersionedEntity, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<VersionedRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<VersionedEntity, AppException>.Success(value: entity));

		Result<string, AppException> result = await BuildAdapter(loader: loader, handler: handler).Handle(
			request: new VersionedRequest(UserId: UserId, ExpectedVersion: 3),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PreconditionFailedException>();

		PreconditionFailedException mismatch = (PreconditionFailedException)result.Error!;
		await Assert.That(value: mismatch.ExpectedVersion).IsEqualTo(expected: 3);
		await Assert.That(value: mismatch.ActualVersion).IsEqualTo(expected: 9);
		await Assert.That(value: mismatch.Id).IsEqualTo(expected: entity.Id)
			.Because(message: "the client needs to know which resource moved under it, not just that one did");

		await Assert.That(value: handler.ReceivedCalls()).IsEmpty()
			.Because(message: "running the handler on a version the caller never saw is the lost update this check exists to prevent");
	}

	[Test]
	public async Task DemandingAVersionFromAnUnversionedEntityIsAWiringError()
	{
		IEntityLoader<VersionedRequest, UnversionedEntity, AppException> loader =
			Substitute.For<IEntityLoader<VersionedRequest, UnversionedEntity, AppException>>();
		IAuthorizedHandler<VersionedRequest, UnversionedEntity, string, AppException> handler =
			Substitute.For<IAuthorizedHandler<VersionedRequest, UnversionedEntity, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<VersionedRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<UnversionedEntity, AppException>.Success(value: new UnversionedEntity()));

		AuthorizedHandlerAdapter<VersionedRequest, UnversionedEntity, string, AppException> adapter =
			new AuthorizedHandlerAdapter<VersionedRequest, UnversionedEntity, string, AppException>(
				loader: loader,
				handler: handler,
				logger: NullLogger<AuthorizedHandlerAdapter<VersionedRequest, UnversionedEntity, string, AppException>>.Instance
			);

		await Assert.That(action: async () => await adapter.Handle(
			request: new VersionedRequest(UserId: UserId, ExpectedVersion: 1),
			ct: CancellationToken.None
		)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task AnErrorTypeTooNarrowToReportAMismatchIsAWiringError()
	{
		IEntityLoader<NarrowErrorRequest, VersionedEntity, ValidationException> loader =
			Substitute.For<IEntityLoader<NarrowErrorRequest, VersionedEntity, ValidationException>>();
		IAuthorizedHandler<NarrowErrorRequest, VersionedEntity, string, ValidationException> handler =
			Substitute.For<IAuthorizedHandler<NarrowErrorRequest, VersionedEntity, string, ValidationException>>();

		loader.LoadAsync(request: Arg.Any<NarrowErrorRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<VersionedEntity, ValidationException>.Success(value: new VersionedEntity { Version = 2 }));

		AuthorizedHandlerAdapter<NarrowErrorRequest, VersionedEntity, string, ValidationException> adapter =
			new AuthorizedHandlerAdapter<NarrowErrorRequest, VersionedEntity, string, ValidationException>(
				loader: loader,
				handler: handler,
				logger: NullLogger<AuthorizedHandlerAdapter<NarrowErrorRequest, VersionedEntity, string, ValidationException>>.Instance
			);

		await Assert.That(action: async () => await adapter.Handle(
			request: new NarrowErrorRequest(UserId: UserId, ExpectedVersion: 1),
			ct: CancellationToken.None
		)).Throws<InvalidOperationException>();

		await Assert.That(value: handler.ReceivedCalls()).IsEmpty();
	}

	[Test]
	public async Task TheEntitylessAdapterRunsTheHandlerOnceAccessIsGranted()
	{
		IEntityLoader<PlainRequest, AppException> loader = Substitute.For<IEntityLoader<PlainRequest, AppException>>();
		IAuthorizedHandler<PlainRequest, string, AppException> handler = Substitute.For<IAuthorizedHandler<PlainRequest, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<PlainRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<UnitResult, AppException>.Success(value: UnitResult.Default));
		handler.HandleAsync(request: Arg.Any<PlainRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<string, AppException>.Success(value: "done"));

		Result<string, AppException> result = await new AuthorizedHandlerAdapter<PlainRequest, string, AppException>(
			loader: loader,
			handler: handler,
			logger: NullLogger<AuthorizedHandlerAdapter<PlainRequest, string, AppException>>.Instance
		).Handle(request: new PlainRequest(UserId: UserId), ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task TheEntitylessAdapterAlsoStopsOnARefusedLoad()
	{
		IEntityLoader<PlainRequest, AppException> loader = Substitute.For<IEntityLoader<PlainRequest, AppException>>();
		IAuthorizedHandler<PlainRequest, string, AppException> handler = Substitute.For<IAuthorizedHandler<PlainRequest, string, AppException>>();

		loader.LoadAsync(request: Arg.Any<PlainRequest>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<UnitResult, AppException>.Failure(error: new NotFoundException(message: "not yours", id: Guid.CreateVersion7())));

		Result<string, AppException> result = await new AuthorizedHandlerAdapter<PlainRequest, string, AppException>(
			loader: loader,
			handler: handler,
			logger: NullLogger<AuthorizedHandlerAdapter<PlainRequest, string, AppException>>.Instance
		).Handle(request: new PlainRequest(UserId: UserId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: handler.ReceivedCalls()).IsEmpty();
	}
}
