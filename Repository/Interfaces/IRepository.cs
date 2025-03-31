using Repository.Models;

namespace Repository.Interfaces
{
    public interface IRepository : IReadOnlyRepository
    {
        Task<ResultResponse<TEntity>> CreateAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> UpdateAsync<TEntity>(TEntity entity, bool ignoreNulls = false)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> SaveAsync<TEntity>(TEntity entity, bool ignoreNullsOnUpdate = false)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity;

        Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkCreateAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkUpdateAsync<TEntity>(IEnumerable<TEntity> entities, bool ignoreNulls = false)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkSaveAsync<TEntity>(IEnumerable<TEntity> entities, bool ignoreNullsOnUpdate = false)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkDeleteAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkDeleteAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkArchiveAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkArchiveAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkRestoreAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity;

        Task<ResultResponse<IEnumerable<TEntity>>> BulkRestoreAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity;
    }
}
