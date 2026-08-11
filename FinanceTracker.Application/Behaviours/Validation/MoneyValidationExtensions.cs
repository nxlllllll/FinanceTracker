using FinanceTracker.Core.ValueObjects;
using FluentValidation;

namespace FinanceTracker.Application.Behaviours.Validation;

/// <summary>
/// Shared rules for monetary fields on commands.
/// </summary>
public static class MoneyValidationExtensions
{
	/// <summary>
	/// Rejects amounts carrying more precision than storage keeps.
	/// </summary>
	public static IRuleBuilderOptions<T, decimal> MustFitAmountScale<T>(this IRuleBuilder<T, decimal> ruleBuilder)
	{
		return ruleBuilder.Must(predicate: amount => MonetaryScale.FitsScale(value: amount, scale: MonetaryScale.Amount))
			.WithMessage(errorMessage: $"Amount cannot have more than {MonetaryScale.Amount} decimal places.");
	}
}
