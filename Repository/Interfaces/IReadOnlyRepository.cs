using Repository.Models;
using System.Reflection;

namespace Repository.Interfaces
{
    public interface IReadOnlyRepository
    {
        PropertyInfo?[]? GetPrimaryKeyDefinition<TEntity>()
            where TEntity : class, new();

        object?[]? GetPrimaryKey<TEntity>(TEntity entity);

        IQueryable<TDTO> GetQueryable<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        IQueryable<IGrouping<TKey, TDTO>> GetGroupedQueryable<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO> SingleAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO?> SingleOrDefaultAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO> FirstAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO?> FirstOrDefaultAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO> LastAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<TDTO?> LastOrDefaultAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<IEnumerable<TDTO>> GetEnumerableAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<IEnumerable<IGrouping<TKey, TDTO>>> GetGroupedEnumerableAsync<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<FetchResponse<TDTO>> GetFetchResponseAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<FetchResponse<IGrouping<TKey, TDTO>>> GetGroupedFetchResponseAsync<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;
    }
}
