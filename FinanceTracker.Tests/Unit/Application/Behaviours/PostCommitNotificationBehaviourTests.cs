using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

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
	public async Task Handle_WhenStagedTwice_ShouldPublishBothInStagingOrder()
	{
		ProbeNotification first = new ProbeNotification(Marker: 100);
		ProbeNotification second = new ProbeNotification(Marker: 200);
		List<INotification> published = [];

		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo =>
		{
			published.Add(item: callInfo.ArgAt<INotification>(position: 0));
			return Task.CompletedTask;
		});

		await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: first);
				_collector.Stage(notification: second);
				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: published).Count().IsEqualTo(expected: 2);
		await Assert.That(value: published[0]).IsEqualTo(expected: first);
		await Assert.That(value: published[1]).IsEqualTo(expected: second);
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

	[Test]
	public async Task Handle_WhenANestedDispatchRuns_ShouldLeaveTheOuterNotificationForTheOuterDispatch()
	{
		ProbeNotification outer = new ProbeNotification(Marker: 10);
		ProbeNotification inner = new ProbeNotification(Marker: 20);
		List<INotification> published = [];

		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo =>
		{
			published.Add(item: callInfo.ArgAt<INotification>(position: 0));
			return Task.CompletedTask;
		});

		await _behaviour.Handle(
			request: new object(),
			next: async ct =>
			{
				_collector.Stage(notification: outer);

				await _behaviour.Handle(
					request: new object(),
					next: _ =>
					{
						_collector.Stage(notification: inner);
						return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
					},
					cancellationToken: ct
				);

				return Result<Guid, DomainException>.Success(value: Guid.NewGuid());
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: published).Count().IsEqualTo(expected: 2);
		await Assert.That(value: published[0]).IsEqualTo(expected: inner);
		await Assert.That(value: published[1]).IsEqualTo(expected: outer);
	}

	[Test]
	public async Task Handle_WhenANestedDispatchStagesNothing_ShouldStillLeaveTheOuterNotificationAlone()
	{
		ProbeNotification outer = new ProbeNotification(Marker: 11);

		await _behaviour.Handle(
			request: new object(),
			next: async ct =>
			{
				_collector.Stage(notification: outer);

				await _behaviour.Handle(
					request: new object(),
					next: _ => Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid())),
					cancellationToken: ct
				);

				return Result<Guid, DomainException>.Success(value: Guid.NewGuid());
			},
			cancellationToken: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenOnePublishThrows_ShouldStillPublishTheRest()
	{
		ProbeNotification failing = new ProbeNotification(Marker: 30);
		ProbeNotification following = new ProbeNotification(Marker: 40);
		List<INotification> attempted = [];

		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo =>
		{
			INotification notification = callInfo.ArgAt<INotification>(position: 0);
			attempted.Add(item: notification);

			if (ReferenceEquals(objA: notification, objB: failing))
				throw new InvalidOperationException(message: "subscriber blew up");

			return Task.CompletedTask;
		});

		await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: failing);
				_collector.Stage(notification: following);
				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: attempted).Count().IsEqualTo(expected: 2);
		await Assert.That(value: attempted[1]).IsEqualTo(expected: following);
	}

	[Test]
	public async Task Handle_WhenNextThrows_ShouldNotLeaveWhatItStagedForWhoeverRunsNext()
	{
		ProbeNotification abandoned = new ProbeNotification(Marker: 70);

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: abandoned);
				throw new InvalidOperationException(message: "commit failed");
			},
			cancellationToken: CancellationToken.None
		));

		await Assert.That(value: _collector.Mark()).IsEqualTo(expected: 0).Because(message: """
			An attempt that threw did not commit, so what it staged describes something that never
			happened. Left behind it sits below every later mark — never published, but shifting the
			index every subsequent dispatch measures itself against, so the collector's depth becomes a
			function of how many attempts failed earlier in the request.
		""");

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAFailedAttemptIsRetriedSuccessfully_ShouldPublishOncePerOutcomeRatherThanOncePerAttempt()
	{
		ProbeNotification notification = new ProbeNotification(Marker: 80);
		List<INotification> published = [];

		_publisher.Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo =>
		{
			published.Add(item: callInfo.ArgAt<INotification>(position: 0));
			return Task.CompletedTask;
		});

		int attempts = 0;

		Task<Result<Guid, DomainException>> Attempt(CancellationToken ct) => _behaviour.Handle(
			request: new object(),
			next: _ =>
			{
				_collector.Stage(notification: notification);

				if (++attempts == 1)
					throw new InvalidOperationException(message: "transient fault at commit");

				return Task.FromResult(Result<Guid, DomainException>.Success(value: Guid.NewGuid()));
			},
			cancellationToken: ct
		);

		try
		{
			await Attempt(ct: CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			await Attempt(ct: CancellationToken.None);
		}

		await Assert.That(value: attempts).IsEqualTo(expected: 2);

		await Assert.That(value: published).Count().IsEqualTo(expected: 1).Because(message: """
			TransientRetryBehaviour is registered outside this one, so a retried request runs the handler
			— and its staging — once per attempt while only one of them commits. Counting per attempt
			rather than per outcome doubles every audit line the moment the database drops a connection
			at commit time, and would double the rows once the audit trail stops being a log.
		""");
	}

	[Test]
	public async Task Handle_WhenANestedDispatchFails_ShouldNotLeaveItsNotificationForTheOuterOne()
	{
		ProbeNotification outer = new ProbeNotification(Marker: 50);
		ProbeNotification abandoned = new ProbeNotification(Marker: 60);

		await _behaviour.Handle(
			request: new object(),
			next: async ct =>
			{
				_collector.Stage(notification: outer);

				await _behaviour.Handle(
					request: new object(),
					next: _ =>
					{
						_collector.Stage(notification: abandoned);
						return Task.FromResult(Result<Guid, DomainException>.Failure(error: new InvalidAmountException(message: "nope")));
					},
					cancellationToken: ct
				);

				return Result<Guid, DomainException>.Success(value: Guid.NewGuid());
			},
			cancellationToken: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);

		await _publisher.DidNotReceive().Publish(
			notification: abandoned,
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
