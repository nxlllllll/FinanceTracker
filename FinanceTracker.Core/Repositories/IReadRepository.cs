using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Core.Repositories;

public interface IReadRepository<out T> where T : IReadModel { }