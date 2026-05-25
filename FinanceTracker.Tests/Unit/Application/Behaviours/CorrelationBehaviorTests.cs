using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Core.Services.Correlation;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class CorrelationBehaviorTests
{
    public sealed record TestCommand : IRequest<string>;

    public sealed record TestCommandWithCorrelation(Guid CorrelationId) : IRequest<string>, IHasCorrelationId;

    private ICorrelationContext _context = null!;
    private CorrelationBehavior<TestCommand, string> _behavior = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _context = Substitute.For<ICorrelationContext>();
        _context.CorrelationId.Returns(returnThis: Guid.CreateVersion7());

        _behavior = new CorrelationBehavior<TestCommand, string>(
            correlationContext: _context,
            logger: Substitute.For<ILogger<CorrelationBehavior<TestCommand, string>>>()
        );
    }

    [Test]
    public async Task Handle_WhenCommandHasCorrelationId_ShouldSetOnContext()
    {
        Guid expected = Guid.CreateVersion7();
        TestCommandWithCorrelation command = new TestCommandWithCorrelation(CorrelationId: expected);

        CorrelationBehavior<TestCommandWithCorrelation, string> behavior = new CorrelationBehavior<TestCommandWithCorrelation, string>(
            correlationContext: _context,
            logger: Substitute.For<ILogger<CorrelationBehavior<TestCommandWithCorrelation, string>>>()
        );

        await behavior.Handle(
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

        CorrelationBehavior<TestCommandWithCorrelation, string> behavior = new CorrelationBehavior<TestCommandWithCorrelation, string>(
            correlationContext: _context,
            logger: Substitute.For<ILogger<CorrelationBehavior<TestCommandWithCorrelation, string>>>()
        );

        await behavior.Handle(
            request: command,
            next: _ => Task.FromResult(result: "ok"),
            cancellationToken: CancellationToken.None
        );

        _context.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
    }

    [Test]
    public async Task Handle_WhenCommandDoesNotHaveCorrelationId_ShouldGenerateFallbackAndSetOnContext()
    {
        await _behavior.Handle(
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

        CorrelationBehavior<TestCommandWithCorrelation, string> behavior = new CorrelationBehavior<TestCommandWithCorrelation, string>(
            correlationContext: _context,
            logger: Substitute.For<ILogger<CorrelationBehavior<TestCommandWithCorrelation, string>>>()
        );

        await behavior.Handle(
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

        await _behavior.Handle(
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
        string result = await _behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: "expected"),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result).IsEqualTo(expected: "expected");
    }
}
