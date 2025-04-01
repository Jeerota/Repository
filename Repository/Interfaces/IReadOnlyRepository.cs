using Repository.Models;
using System.Linq.Expressions;
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

        IQueryable<TDTO> GetQueryable<TDTO, TEntity>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity;

        IQueryable<IGrouping<TKey, TDTO>> GetGroupedQueryable<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        IQueryable<IGrouping<TKey, TDTO>> GetGroupedQueryable<TDTO, TEntity, TKey>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Expression<Func<TDTO, TKey>> groupBy,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity;

        Task<FetchResponse<TDTO>> GetFetchResponseAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<FetchResponse<TDTO>> GetFetchResponseAsync<TDTO, TEntity>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            int page,
            int pageSize,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity;

        Task<FetchResponse<IGrouping<TKey, TDTO>>> GetGroupedFetchResponseAsync<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity;

        Task<FetchResponse<IGrouping<TKey, TDTO>>> GetGroupedFetchResponseAsync<TDTO, TEntity, TKey>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Expression<Func<TDTO, TKey>> groupBy,
            int page,
            int pageSize,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity;
    }
}
