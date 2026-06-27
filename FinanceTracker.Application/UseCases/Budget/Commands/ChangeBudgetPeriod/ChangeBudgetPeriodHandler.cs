using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
    IBudgetReadRepository budgetReadRepository,
    IBudgetWriteRepository budgetWriteRepository,
    IBudgetProgressWriteRepository budgetProgressWriteRepository,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IDateProvider dateProvider,
    ILogger<ChangeBudgetPeriodHandler> logger
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
    public async Task<Result<Guid, DomainException>> HandleAsync(
        ChangeBudgetPeriodCommand command,
        Core.Domains.Budget.Budget entity,
        CancellationToken ct = default)
    {
        Result<Unit, DomainException> domainResult = entity.ChangePeriod(from: command.From, to: command.To);
        if (domainResult.IsFailure)
            return Result<Guid, DomainException>.Failure(error: domainResult.Error!);

        bool hasOverlap;

        try
        {
            hasOverlap = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
            {
                bool overlap = await budgetReadRepository.HasOverlappingAsync(
                    userId: command.UserId,
                    categoryId: entity.CategoryId,
                    from: command.From,
                    to: command.To,
                    excludeBudgetId: entity.Id,
                    ct: ct
                );

                if (overlap)
                    return true;

                await budgetWriteRepository.ChangePeriodAsync(
                    budgetId: entity.Id,
                    from: command.From,
                    to: command.To,
                    expectedVersion: entity.RowVersion,
                    ct: ct
                );

                await budgetProgressWriteRepository.RecalculateForBudgetAsync(
                    budgetId: entity.Id,
                    userId: command.UserId,
                    categoryId: entity.CategoryId,
                    fromDate: command.From,
                    toDate: command.To,
                    ct: ct
                );

                return false;
            },
            onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to change period for budget {entity.Id} ({command.From} > {command.To})."),
            ct: ct);
        }
        catch (UniqueConstraintException)
        {
            hasOverlap = true;
        }

        if (hasOverlap)
            return Result<Guid, DomainException>.Failure(error: new OverlappingBudgetException(message: "A budget for this category already exists in the specified period."));

        await publisher.Publish(notification: new BudgetPeriodChangedNotification(
            BudgetId: entity.Id,
            UserId: entity.UserId,
            NewFrom: command.From,
            NewTo: command.To,
            OccurredAt: dateProvider.UtcNow
        ), cancellationToken: ct);

        return Result<Guid, DomainException>.Success(value: entity.Id);
    }
}