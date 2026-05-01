using FinanceTracker.Application.Users.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RegisterUserHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IUserReadRepository _userReadRepository = null!;
    private RegisterUserHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _userReadRepository = Substitute.For<IUserReadRepository>();
        _userWriteRepository = Substitute.For<IUserWriteRepository>();
        _handler = new RegisterUserHandler(
            userWriteRepository: _userWriteRepository,
            userReadRepository: _userReadRepository,
            dateProvider: FakeDateProvider.Default
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        _userReadRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _userWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            user: Arg.Is<FinanceTracker.Core.Domains.User.User>(u =>
                u.Email == "test@test.com" &&
                u.BaseCurrency == "RUB"
            ),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnUserId()
    {
        _userReadRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult<FinanceTracker.Core.Domains.User.User?>(null));

        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

        await Assert.That(value: result).IsNotDefault();
    }

    [Test]
    public async Task Handle_WithDuplicateEmail_ShouldThrowDuplicateEmailException()
    {
        FinanceTracker.Core.Domains.User.User existingUser = UserFactory.Create();

        _userReadRepository.GetByEmailAsync(
            email: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: existingUser);

        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        await Assert.That(action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)).Throws<EmailException>();
    }
}