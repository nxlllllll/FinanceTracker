using FinanceTracker.Application.Users.Commands.ChangeUserEmail;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserEmailHandlerTests
{
    private IUserRepository _userRepository = null!;
    private ChangeUserEmailHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _handler = new ChangeUserEmailHandler(userRepository: _userRepository);
    }

    private static FinanceTracker.Core.Domains.User.User CreateUser()
    {
        return FinanceTracker.Core.Domains.User.User.Register(
            email: "test@test.com",
            passwordHash: "hash",
            baseCurrencyCode: "RUB"
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldChangeEmail()
    {
        FinanceTracker.Core.Domains.User.User user = CreateUser();
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);

        _userRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

        ChangeUserEmailCommand command = new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com");

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _userRepository.Received(requiredNumberOfCalls: 1).ChangeEmailAsync(
            userId: user.Id,
            newEmail: "new@test.com",
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenUserNotFound_ShouldThrowNotFoundException()
    {
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

        ChangeUserEmailCommand command = new ChangeUserEmailCommand(UserId: Guid.NewGuid(), NewEmail: "new@test.com");

        await Assert.That(action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WithDuplicateEmail_ShouldThrowDuplicateEmailException()
    {
        FinanceTracker.Core.Domains.User.User user = CreateUser();
        FinanceTracker.Core.Domains.User.User anotherUser = FinanceTracker.Core.Domains.User.User.Register(
            email: "new@test.com",
            passwordHash: "hash",
            baseCurrencyCode: "RUB"
        );

        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);

        _userRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: anotherUser);

        ChangeUserEmailCommand command = new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com");

        await Assert.That(action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)).Throws<EmailException>();
    }

}