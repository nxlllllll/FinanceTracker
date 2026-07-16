using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed record ProbeNotification(int Marker) : INotification;

public sealed class PostCommitNotificationBehaviourTests
{
	private PostCommitNotificationCollector _collector = null!;
	private IPublisher _publisher = null!;
	private PostCommitNotificationBehaviour<object, Result<Guid, DomainException>> _behaviour = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_collector = new PostCommitNotificationCollector();
		_publisher = Substitute.For<IPublisher>();

		_behaviour = new PostCommitNotificationBehaviour<object, Result<Guid, DomainException>>(
			notifications: _collector,
			publisher: _publisher,
			logger: Substitute.For<ILogger<PostCommitNotificationBehaviour<object, Result<Guid, DomainException>>>>()
		);
	}

	[Test]
	public async Task Handle_WhenNextSucceedsAndNotificationStaged_ShouldPublishIt()
	{
		ProbeNotification notification = new ProbeNotification(Marker: 1);

		await _behaviour.Handle(
			request: new object(),
			next: ct =>
			{
				_collector.Stage(notification: notification);
				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: notification,
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNextSucceedsButNothingStaged_ShouldNotPublish()
	{
		await _behaviour.Handle(
			request: new object(),
			next: _ => Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNextFails_ShouldNotPublishEvenIfSomethingWasStaged()
	{
		ProbeNotification notification = new ProbeNotification(Marker: 2);

		await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: notification);
				return Task.FromResult(Result<Guid, DomainException>.Failure(error: new InvalidAmountException(message: "nope")));
			},
			cancellationToken: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldReturnWhateverNextReturned_Unchanged()
	{
		Result<Guid, DomainException> expected = Result<Guid, DomainException>.Success(value: Guid.NewGuid());

		Result<Guid, DomainException> actual = await _behaviour.Handle(
			request: new object(),
			next: _ => Task.FromResult(expected),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: actual).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task Handle_WhenPublishThrows_ShouldSwallowAndStillReturnResponse()
	{
		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ => throw new InvalidOperationException(message: "downstream handler failed"));

		Result<Guid, DomainException> expected = Result<Guid, DomainException>.Success(value: Guid.NewGuid());

		Result<Guid, DomainException> actual = await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: new ProbeNotification(Marker: 3));
				return Task.FromResult(expected);
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: actual).IsEqualTo(expected: expected).Because(message: """
			A publish failure is an observability concern (logged inside the behaviour), not a
			reason to fail a request that already succeeded and committed.
		""");
	}

	[Test]
	public async Task Handle_WhenStagedTwiceBeforeNextReturns_ShouldPublishOnlyTheLastOne()
	{
		ProbeNotification stale = new ProbeNotification(Marker: 100);
		ProbeNotification fresh = new ProbeNotification(Marker: 200);

		await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: stale);
				_collector.Stage(notification: fresh);
				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(notification: fresh, cancellationToken: Arg.Any<CancellationToken>());
		await _publisher.DidNotReceive().Publish(notification: stale, cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPublishWithNonCancellableToken()
	{
		ProbeNotification notification = new ProbeNotification(Marker: 4);
		CancellationToken? capturedToken = null;

		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo =>
		{
			capturedToken = callInfo.ArgAt<CancellationToken>(position: 1);
			return Task.CompletedTask;
		});

		using CancellationTokenSource cts = new CancellationTokenSource();

		await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: notification);
				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: cts.Token
		);

		await Assert.That(value: capturedToken).IsEqualTo(expected: CancellationToken.None);
	}
}
