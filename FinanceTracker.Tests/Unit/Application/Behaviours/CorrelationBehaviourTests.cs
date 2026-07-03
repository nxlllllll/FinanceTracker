using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Core.Services.Correlation;
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
		_context.CorrelationId.Returns(returnThis: Guid.CreateVersion7());

		_behaviour = new CorrelationBehaviour<TestCommand, string>(
			correlationContext: _context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommand, string>>>()
		);
	}

	[Test]
	public async Task Handle_WhenCommandHasCorrelationId_ShouldSetOnContext()
	{
		Guid expected = Guid.CreateVersion7();
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: expected);

		CorrelationBehaviour<TestCommandWithCorrelation, string> behaviour = new CorrelationBehaviour<TestCommandWithCorrelation, string>(
			correlationContext: _context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommandWithCorrelation, string>>>()
		);

		await behaviour.Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: expected);
	}

	[Test]
	public async Task Handle_WhenCorrelationIdIsEmpty_ShouldGenerateFallbackAndSetOnContext()
	{
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: Guid.Empty);

		CorrelationBehaviour<TestCommandWithCorrelation, string> behaviour = new CorrelationBehaviour<TestCommandWithCorrelation, string>(
			correlationContext: _context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommandWithCorrelation, string>>>()
		);

		await behaviour.Handle(
			request: command,
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task Handle_WhenCommandDoesNotHaveCorrelationId_ShouldGenerateFallbackAndSetOnContext()
	{
		await _behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: "ok"),
			cancellationToken: CancellationToken.None
		);

		_context.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task Handle_WhenCommandHasCorrelationId_ShouldSetExactId_NotGenerated()
	{
		Guid externalId = Guid.CreateVersion7();
		TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: externalId);

		CorrelationBehaviour<TestCommandWithCorrelation, string> behaviour = new CorrelationBehaviour<TestCommandWithCorrelation, string>(
			correlationContext: _context,
			logger: Substitute.For<ILogger<CorrelationBehaviour<TestCommandWithCorrelation, string>>>()
		);

		await behaviour.Handle(
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
