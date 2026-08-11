using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Core.Observability.Correlation;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class CorrelationBehaviourTests
{
	public sealed record TestCommand : IRequest<string>;

	public sealed record TestCommandWithCorrelation(Guid CorrelationId) : IRequest<string>, IHasCorrelationId;

	private ICorrelationContext _context = null!;
	private CorrelationBehaviour<TestCommand, string> _behaviour = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_context = Substitute.For<ICorrelationContext>();
		_context.CorrelationId.Returns(returnThis: Guid.Empty);

		_behaviour = new CorrelationBehaviour<TestCommand, string>(
			correlationContext: _context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommand, string>>>()
		);
	}

	private static CorrelationBehaviour<TestCommandWithCorrelation, string> BuildBehaviourFor(ICorrelationContext context)
	{
		return new CorrelationBehaviour<TestCommandWithCorrelation, string>(
			correlationContext: context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommandWithCorrelation, string>>>()
		);
	}

	[Test]
	public async Task Handle_WhenCommandHasCorrelationId_ShouldSetOnContext()
	{
		Guid expected = Guid.CreateVersion7();
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: expected);

		await BuildBehaviourFor(context: _context).Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: expected);
	}

	[Test]
	public async Task Handle_WhenCorrelationIdIsEmptyAndNothingAlreadySet_ShouldGenerateFallbackAndSetOnContext()
	{
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: Guid.Empty);

		await BuildBehaviourFor(context: _context).Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task Handle_WhenCommandDoesNotHaveCorrelationId_AndNothingAlreadySet_ShouldGenerateFallbackAndSetOnContext()
	{
		await _behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task Handle_WhenCommandDoesNotHaveCorrelationId_AndAlreadySetByAnEarlierStage_ShouldNotOverwriteIt()
	{
		ICorrelationContext alreadySetContext = Substitute.For<ICorrelationContext>();
		alreadySetContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());

		CorrelationBehaviour<TestCommand, string> behaviour = new CorrelationBehaviour<TestCommand, string>(
			correlationContext: alreadySetContext,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommand, string>>>()
		);

		await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		alreadySetContext.DidNotReceive().Set(correlationId: Arg.Any<Guid>());
	}

	[Test]
	public async Task Handle_WhenCommandHasCorrelationId_ShouldOverwriteEvenIfAlreadySetByAnEarlierStage()
	{
		ICorrelationContext alreadySetContext = Substitute.For<ICorrelationContext>();
		alreadySetContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());

		Guid explicitId = Guid.CreateVersion7();
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: explicitId);

		await BuildBehaviourFor(context: alreadySetContext).Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		alreadySetContext.Received(requiredNumberOfCalls: 1).Set(correlationId: explicitId);
	}

	[Test]
	public async Task Handle_WhenCommandHasCorrelationId_ShouldSetExactId_NotGenerated()
	{
		Guid externalId = Guid.CreateVersion7();
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: externalId);

		await BuildBehaviourFor(context: _context).Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: externalId);
		_context.DidNotReceive().Set(correlationId: Arg.Is<Guid>(x => x != externalId));
	}

	[Test]
	public async Task Handle_ShouldAlwaysCallNext()
	{
		bool nextCalled = false;

		await _behaviour.Handle(
			request: new TestCommand(),
			next: _ =>
			{
				nextCalled = true;
				return Task.FromResult(result: "ok");
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: nextCalled).IsTrue();
	}

	[Test]
	public async Task Handle_ShouldReturnNextResult()
	{
		string result = await _behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: "expected"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: "expected");
	}
}
