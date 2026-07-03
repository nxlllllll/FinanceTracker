using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

/// <summary>
/// Published by <see cref="RegisterUserHandler"/> after a user is successfully created.
/// </summary>
/// <param name="UserId">ID of the newly created user.</param>
/// <param name="Email">Email address of the new user.</param>
/// <param name="BaseCurrency">Base currency selected at registration.</param>
/// <param name="OccurredAt">UTC timestamp of the registration.</param>
public sealed record UserRegisteredNotification(
	Guid UserId,
	Email Email,
	Core.ValueObjects.Currency BaseCurrency,
	DateTimeOffset OccurredAt
) : INotification;
