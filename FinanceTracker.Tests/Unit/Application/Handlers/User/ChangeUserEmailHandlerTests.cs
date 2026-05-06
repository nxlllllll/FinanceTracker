using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserEmailHandlerTests
{
    private IUserReadRepository _userReadRepository = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private ChangeUserEmailHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _userReadRepository = Substitute.For<IUserReadRepository>();
        _userWriteRepository = Substitute.For<IUserWriteRepository>();
        _handler = new ChangeUserEmailHandler(
            userReadRepository: _userReadRepository,
            userWriteRepository: _userWriteRepository
        );
    }

    [Test]
    public async Task HandleAsync_WithValidCommand_ShouldChangeEmail()
    {
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

        _userReadRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

        await _handler.HandleAsync(
            command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
            user: user,
            ct: CancellationToken.None
        );

        await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeEmailAsync(
            userId: user.Id,
            newEmail: Email.Create(value: "new@test.com").Value,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WithDuplicateEmail_ShouldThrowEmailException()
    {
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
        FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

        _userReadRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: anotherUser);

        Result<Guid, DomainException> result = await _handler.HandleAsync(
            command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
            user: user,
            ct: CancellationToken.None
        );
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }
}