using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Categories.Authorization;

internal static class CategoryHandlerRegistration
{
	internal static IServiceCollection RegisterCategoryHandlers(this IServiceCollection services)
	{
		services.AddScoped<CategoryLoader>();
		services.AddScoped<IEntityLoader<ArchiveCategoryCommand, Category, NotFoundException>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);
		services.AddScoped<IEntityLoader<UnarchiveCategoryCommand, Category, NotFoundException>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);
		services.AddScoped<IEntityLoader<RenameCategoryCommand, Category, NotFoundException>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);

		services.AddScoped<IAuthorizedHandler<ArchiveCategoryCommand, Category, Guid, DomainException>, ArchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveCategoryCommand, Category, Guid, DomainException>, UnarchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<RenameCategoryCommand, Category, Guid, DomainException>, RenameCategoryHandler>();

		services.AddScoped<IRequestHandler<ArchiveCategoryCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ArchiveCategoryCommand, Category, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<UnarchiveCategoryCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<UnarchiveCategoryCommand, Category, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<RenameCategoryCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<RenameCategoryCommand, Category, Guid, DomainException>>();

		return services;
	}
}