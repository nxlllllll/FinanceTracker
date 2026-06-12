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
        Core.Domains.Budget.Budget budget,
        CancellationToken ct = default)
    {
        bool hasOverlap = await budgetReadRepository.HasOverlappingAsync(
            userId: command.UserId,
            categoryId: budget.CategoryId,
            from: command.From,
            to: command.To,
            excludeBudgetId: budget.Id,
            ct: ct
        );

        if (hasOverlap)
            return Result<Guid, DomainException>.Failure(error: new OverlappingBudgetException(message: "A budget for this category already exists in the specified period."));

        Result<Unit, DomainException> result = budget.ChangePeriod(from: command.From, to: command.To);
        if (result.IsFailure)
            return Result<Guid, DomainException>.Failure(error: result.Error!);

        await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            await budgetWriteRepository.ChangePeriodAsync(
                budgetId: budget.Id,
                from: command.From,
                to: command.To,
                expectedVersion: budget.RowVersion,
                ct: ct
            );

            await budgetProgressWriteRepository.RecalculateForBudgetAsync(
                budgetId: budget.Id,
                userId: command.UserId,
                categoryId: budget.CategoryId,
                fromDate: command.From,
                toDate: command.To,
                ct: ct
            );
        },
        onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to change period for budget {budget.Id} ({command.From} > {command.To})."),
        ct: ct);

        await publisher.Publish(notification: new BudgetPeriodChangedNotification(
            BudgetId: budget.Id,
            UserId: budget.UserId,
            NewFrom: command.From,
            NewTo: command.To,
            OccurredAt: dateProvider.UtcNow
        ), cancellationToken: ct);
        
        return Result<Guid, DomainException>.Success(value: budget.Id);
    }
}
