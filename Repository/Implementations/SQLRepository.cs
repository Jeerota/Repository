using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Repository.Interfaces;
using Repository.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Repository.Implementations
{
    public class SQLRepository<TContext>(IDbContextFactory<TContext> dbContextFactory, IApplicationContext applicationContext) : IRepository, IReadOnlyRepository, IDisposable
        where TContext : DbContext
    {
        protected readonly IDbContextFactory<TContext> _dbContextFactory = dbContextFactory;
        protected readonly IApplicationContext _applicationContext = applicationContext;

        private static IQueryable<TEntity> IncludeInQuery<TEntity>(DbContext dbContext, Expression<Func<TEntity, object>>[] include, DateTime? temporalAsOf = null) 
            where TEntity : class => 
            include.Aggregate(temporalAsOf.HasValue
                ? dbContext.Set<TEntity>().TemporalAsOf(temporalAsOf.Value)
                : dbContext.Set<TEntity>().AsNoTracking(), (current, item) => EvaluateInclude(current, item));

        private static IQueryable<TEntity> EvaluateInclude<TEntity>(IQueryable<TEntity> current, Expression<Func<TEntity, object>> item) where TEntity : class
        {
            if (item.Body is MethodCallExpression expression)
            {
                var arguments = expression.Arguments;
                if (arguments.Count > 1)
                {
                    var navigationPath = string.Empty;
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        var arg = arguments[i];
                        var path = arg.ToString()[(arg.ToString().IndexOf('.') + 1)..];

                        navigationPath += (i > 0 ? "." : string.Empty) + path;
                    }
                    return current.Include(navigationPath);
                }
            }

            return current.Include(item);
        }

        private static IQueryable<TDTO> GetQueryable<TDTO, TEntity>(DbContext dbContext, LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity =>
            GetQueryable(dbContext, lookupRequest.GetPredicate(),
                lookupRequest.Select,
                lookupRequest.GetOrderBy(),
                lookupRequest.TemporalAsOf,
                lookupRequest.GetInclude());

        private static IQueryable<TDTO> GetQueryable<TDTO, TEntity>(DbContext dbContext, 
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy, 
            DateTime? temporalAsOf,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity
        {
            var query = temporalAsOf.HasValue
                ? dbContext.Set<TEntity>().TemporalAsOf(temporalAsOf.Value)
                : dbContext.Set<TEntity>().AsNoTracking();

            query = query.Where(predicate);

            if (include.Any())
                query = IncludeInQuery(dbContext, include, temporalAsOf);

            var convertedQuery = query.Select(select);

            if (orderBy != null)
                convertedQuery = orderBy(convertedQuery);

            return convertedQuery;
        }

        private readonly string[] _ignoreProperties = ["ModifiedBy", "ModifiedOn", "IsArchived"];

        private bool UpdateEntity<TEntity>(DbContext dbContext, TEntity source, ref TEntity target, bool ignoreNulls, Type? parentType = null)
        {
            bool isChanged = false;
            var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
            var navigations = entityType?.GetNavigations();

            foreach(var property in typeof(TEntity).GetProperties())
            {
                if (parentType == property.PropertyType)
                    continue;

                var changedValue = property.GetValue(source);
                var existingValue = target == null ? null : property.GetValue(target);
                Type baseType = property.PropertyType.IsGenericType
                    ? property.PropertyType.GetGenericArguments()[0]
                    : property.PropertyType;

                if(navigations?.Any(x => x.PropertyInfo!.Name == property.Name) ?? false)
                {
                    if (changedValue == null)
                        continue;

                    if(property.PropertyType.IsGenericType
                        && property.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                    {
                        var changedEntities = (IEnumerable<dynamic>)changedValue;
                        var childEntities = Activator.CreateInstance(typeof(Collection<>).MakeGenericType(baseType));
                        foreach(var sourceChildEntity in changedEntities)
                        {
                            var primaryKey = GetPrimaryKey(dbContext, sourceChildEntity);
                            var targetChildEntity = this.GetType()
                                !.GetMethod(nameof(FindAsync))
                                !.MakeGenericMethod([baseType])
                                .Invoke(this, [primaryKey]);

                            if(targetChildEntity == null)
                            {
                                isChanged = true;
                                var entry = dbContext.Entry(sourceChildEntity);
                                dbContext.Add(entry);
                            }
                            else if ((bool)this.GetType()
                                !.GetMethod(nameof(UpdateEntity))
                                !.MakeGenericMethod([baseType])
                                .Invoke(this, [dbContext, sourceChildEntity, targetChildEntity, ignoreNulls, typeof(TEntity)])!)
                            {
                                isChanged = true;
                                var entry = dbContext.Entry(targetChildEntity);
                                dbContext.Update(entry);
                            }

                            childEntities!.GetType()!.GetMethod("Add")!.Invoke(childEntities, [sourceChildEntity]);
                        }

                        property.SetValue(target, childEntities);
                    }
                    else
                    {
                        var primaryKey = GetPrimaryKey(dbContext, changedValue);
                        var targetChildEntity = this.GetType()
                            !.GetMethod(nameof(FindAsync))
                            !.MakeGenericMethod([baseType])
                            .Invoke(this, [primaryKey]);

                        if(targetChildEntity == null)
                        {
                            isChanged = true;
                            var entry = dbContext.Entry(changedValue);
                            dbContext.Add(entry);

                            property.SetValue(target, changedValue);
                        }
                        else if ((bool)this.GetType()
                                !.GetMethod(nameof(UpdateEntity))
                                !.MakeGenericMethod([baseType])
                                .Invoke(this, [dbContext, changedValue, targetChildEntity, ignoreNulls, typeof(TEntity)])!)
                        {
                            isChanged = true;
                            var entry = dbContext.Entry(targetChildEntity);
                            dbContext.Update(entry);

                            property.SetValue(target, changedValue);
                        }
                    }
                }
                else
                {
                    if (_ignoreProperties.Contains(property.Name)
                        || (ignoreNulls && changedValue == null)
                        || (existingValue?.Equals(changedValue) ?? changedValue == null))
                        continue;

                    isChanged = true;

                    var entry  = dbContext.Entry(target ?? source!);
                    dbContext.Add(entry);

                    if (target != null)
                        property.SetValue(target, changedValue);
                    else
                        target = source;
                }
            }

            return isChanged;
        }

        private readonly EntityState[] _changeStates = [EntityState.Added, EntityState.Modified,  EntityState.Deleted];

        private async Task SaveChangesAsync(DbContext dbContext)
        {
            var entries = dbContext.ChangeTracker.Entries()
                .Where(e => _changeStates.Any(s => s == e.State))
                .ToList();

            if (entries.Count < 1)
                return;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        SetCreatedProperties(entry);
                        break;
                    case EntityState.Modified:
                        SetModifiedProperties(entry);
                        break;
                    case EntityState.Deleted:
                        break;
                    default:
                        throw new NotImplementedException($"{entry.State} is not implemented for saving.");
                }
            }

            await dbContext.SaveChangesAsync();

            foreach (var entry in entries)
                entry.State = EntityState.Detached;
        }

        private static void SetEntityProperty(EntityEntry entry, string propertyName, object value)
        {
            PropertyEntry? property = entry.Properties.SingleOrDefault(x => x.Metadata.Name == propertyName);

            if(property != null)
                property.CurrentValue = value;
        }

        private Guid GetCurrentUser() =>
            _applicationContext?.UserId ?? Guid.Empty;

        private static void SetEntityProperties(EntityEntry entry, Dictionary<string, object> properties)
        {
            foreach (var property in properties)
                SetEntityProperty(entry, property.Key, property.Value);
        }

        private void SetCreatedProperties(EntityEntry entry)
        {
            Guid userId = GetCurrentUser();
            DateTime now = DateTime.UtcNow;
            SetEntityProperties(entry, new Dictionary<string, object>
            {
                { "ModifiedOn", now },
                { "ModifiedBy", userId }
            });
        }

        private void SetModifiedProperties(EntityEntry entry)
        {
            Guid userId = GetCurrentUser();
            DateTime now = DateTime.UtcNow;
            SetEntityProperties(entry, new Dictionary<string, object>
            {
                { "ModifiedOn", now },
                { "ModifiedBy", userId }
            });
        }

        public PropertyInfo?[]? GetPrimaryKeyDefinition<TEntity>()
            where TEntity : class, new()
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            TEntity entity = new();
            return dbContext.Entry(entity)
                ?.Metadata
                ?.FindPrimaryKey()
                ?.Properties
                .Select(x => typeof(TEntity).GetProperty(x.Name))
                .ToArray();
        }

        public object?[]? GetPrimaryKey<TEntity>(TEntity entity)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return GetPrimaryKey(dbContext, entity);
        }

        private static object?[]? GetPrimaryKey<TEntity>(DbContext dbContext, TEntity entity)
        {
            if (entity == null)
                return null;

            return dbContext.Entry(entity)
                ?.Metadata
                ?.FindPrimaryKey()
                ?.Properties
                .Select(x => typeof(TEntity).GetProperty(x.Name)?.GetValue(entity))
                .ToArray();
        }

        private static async Task<TEntity?> FindAsync<TEntity>(DbContext dbContext, object?[]? primaryKey)
            where TEntity : BaseEntity
        {
            return await dbContext.FindAsync<TEntity>(primaryKey);
        }

        public IQueryable<TDTO> GetQueryable<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return GetQueryable(dbContext, lookupRequest);
        }

        public IQueryable<TDTO> GetQueryable<TDTO, TEntity>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return GetQueryable(dbContext, predicate, select, orderBy, temporalAsOf, include);
        }

        public IQueryable<IGrouping<TKey, TDTO>> GetGroupedQueryable<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return GetQueryable(dbContext, lookupRequest).GroupBy(lookupRequest.GetGroupBy<TKey>());
        }

        public IQueryable<IGrouping<TKey, TDTO>> GetGroupedQueryable<TDTO, TEntity, TKey>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Expression<Func<TDTO, TKey>> groupBy,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return GetQueryable(dbContext, predicate, select, orderBy, temporalAsOf, include).GroupBy(groupBy);
        }

        public async Task<FetchResponse<TDTO>> GetFetchResponseAsync<TDTO, TEntity>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var query = GetQueryable(dbContext, lookupRequest);

            return new FetchResponse<TDTO>()
            {
                Page = lookupRequest.Page,
                PageSize = lookupRequest.PageSize!.Value,
                TotalRecords = await query.CountAsync(),
                Records = await query.Skip(lookupRequest.Page * lookupRequest.PageSize.Value).Take(lookupRequest.PageSize.Value).ToListAsync()
            };
        }

        public async Task<FetchResponse<TDTO>> GetFetchResponseAsync<TDTO, TEntity>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            int page,
            int pageSize,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var query = GetQueryable(dbContext, predicate, select, orderBy, temporalAsOf, include);

            return new FetchResponse<TDTO>()
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = await query.CountAsync(),
                Records = await query.Skip(page * pageSize).Take(pageSize).ToListAsync()
            };
        }

        public async Task<FetchResponse<IGrouping<TKey, TDTO>>> GetGroupedFetchResponseAsync<TDTO, TEntity, TKey>(LookupRequest<TDTO, TEntity> lookupRequest)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var query = GetQueryable(dbContext, lookupRequest).GroupBy(lookupRequest.GetGroupBy<TKey>());

            return new FetchResponse<IGrouping<TKey, TDTO>>()
            {
                Page = lookupRequest.Page,
                PageSize = lookupRequest.PageSize!.Value,
                TotalRecords = await query.CountAsync(),
                Records = await query.Skip(lookupRequest.Page * lookupRequest.PageSize.Value).Take(lookupRequest.PageSize.Value).ToListAsync()
            };
        }

        public async Task<FetchResponse<IGrouping<TKey, TDTO>>> GetGroupedFetchResponseAsync<TDTO, TEntity, TKey>(Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TDTO>> select,
            Expression<Func<TDTO, TKey>> groupBy,
            int page,
            int pageSize,
            Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>? orderBy = null,
            DateTime? temporalAsOf = null,
            params Expression<Func<TEntity, object>>[] include)
            where TEntity : BaseEntity
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var query = GetQueryable(dbContext, predicate, select, orderBy, temporalAsOf, include).GroupBy(groupBy);

            return new FetchResponse<IGrouping<TKey, TDTO>>()
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = await query.CountAsync(),
                Records = await query.Skip(page * pageSize).Take(pageSize).ToListAsync()
            };
        }

        public async Task<ResultResponse<TEntity>> CreateAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await CreateAsync(dbContext, entity, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> CreateAsync<TEntity>(DbContext dbContext, TEntity entity, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey);

                if (existingEntity != null)
                    throw new InvalidOperationException($"{typeof(TEntity).Name} with primary key already exists. Primary Key: {string.Join(", ", primaryKey!)}");

                dbContext.Add(entity);

                if(saveChanges)
                    await SaveChangesAsync(dbContext);

                resultResponse.Record = entity;
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<TEntity>> UpdateAsync<TEntity>(TEntity entity, bool ignoreNulls = false)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await UpdateAsync(dbContext, entity, ignoreNulls, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> UpdateAsync<TEntity>(DbContext dbContext, TEntity entity, bool ignoreNulls, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey)
                    ?? throw new InvalidOperationException($"{typeof(TEntity).Name} with primary key does not exist. Primary Key: {string.Join(", ", primaryKey!)}");

                if (!UpdateEntity(dbContext, entity, ref existingEntity, ignoreNulls))
                    resultResponse.Warning($"{typeof(TEntity).Name} with primary key is unchanged. Primary Key: {string.Join(", ", primaryKey!)}");

                dbContext.Update(existingEntity);

                if(saveChanges)
                    await SaveChangesAsync(dbContext);

                resultResponse.Record = existingEntity;
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<TEntity>> SaveAsync<TEntity>(TEntity entity, bool ignoreNullsOnUpdate = false)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await SaveAsync(dbContext, entity, ignoreNullsOnUpdate, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> SaveAsync<TEntity>(DbContext dbContext, TEntity entity, bool ignoreNullsOnUpdate = false, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey);

                if (existingEntity == null)
                    dbContext.Add(entity);
                else
                {
                    if (!UpdateEntity(dbContext, entity, ref existingEntity, ignoreNullsOnUpdate))
                        resultResponse.Warning($"{typeof(TEntity).Name} with primary key is unchanged. Primary Key: {string.Join(", ", primaryKey!)}");

                    dbContext.Update(existingEntity);
                }

                if(saveChanges)
                    await SaveChangesAsync(dbContext);

                resultResponse.Record = existingEntity ?? entity;
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await DeleteAsync(dbContext, entity, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(DbContext dbContext, TEntity entity, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
            return await DeleteAsync<TEntity>(dbContext, primaryKey, saveChanges);
        }

        public async Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await DeleteAsync<TEntity>(dbContext, primaryKey, saveChanges: true);
        }
        

        private async Task<ResultResponse<TEntity>> DeleteAsync<TEntity>(DbContext dbContext, object?[]? primaryKey, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey)
                    ?? throw new InvalidOperationException($"{typeof(TEntity).Name} with primary key does not exist. Primary Key: {string.Join(", ", primaryKey!)}");

                dbContext.Remove(existingEntity);

                if (saveChanges)
                    await SaveChangesAsync(dbContext);
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await ArchiveAsync(dbContext, entity, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(DbContext dbContext, TEntity entity, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
            return await ArchiveAsync<TEntity>(dbContext, primaryKey, saveChanges);
        }

        public async Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await ArchiveAsync<TEntity>(dbContext, primaryKey, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> ArchiveAsync<TEntity>(DbContext dbContext, object?[]? primaryKey, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey) 
                    ?? throw new InvalidOperationException($"{typeof(TEntity).Name} with primary key does not exist. Primary Key: {string.Join(", ", primaryKey!)}");

                if (existingEntity.IsArchived)
                    resultResponse.Warning($"{typeof(TEntity).Name} with primary key is already archived. Primary Key: {string.Join(", ", primaryKey!)}");

                existingEntity.IsArchived = true;

                if (saveChanges)
                    await SaveChangesAsync(dbContext);

                resultResponse.Record = existingEntity;
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(TEntity entity)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await RestoreAsync(dbContext, entity, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(DbContext dbContext, TEntity entity, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            object?[]? primaryKey = GetPrimaryKey(dbContext, entity);
            return await RestoreAsync<TEntity>(dbContext, primaryKey, saveChanges);
        }

        public async Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(object?[]? primaryKey)
            where TEntity : BaseEntity
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await RestoreAsync<TEntity>(dbContext, primaryKey, saveChanges: true);
        }

        private async Task<ResultResponse<TEntity>> RestoreAsync<TEntity>(DbContext dbContext, object?[]? primaryKey, bool saveChanges = true)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<TEntity>();
            try
            {
                TEntity? existingEntity = await FindAsync<TEntity>(dbContext, primaryKey)
                    ?? throw new InvalidOperationException($"{typeof(TEntity).Name} with primary key does not exist. Primary Key: {string.Join(", ", primaryKey!)}");

                if (existingEntity.IsArchived)
                    resultResponse.Warning($"{typeof(TEntity).Name} with primary key is not archived. Primary Key: {string.Join(", ", primaryKey!)}");

                existingEntity.IsArchived = false;

                if (saveChanges)
                    await SaveChangesAsync(dbContext);

                resultResponse.Record = existingEntity;
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkCreateAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                List<TEntity> records = [];
                foreach (var entity in entities)
                {
                    var result = await CreateAsync(dbContext, entity, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;

                    if (result.Record != null)
                        records.Add(result.Record);
                }

                if (resultResponse.Successful)
                {
                    await SaveChangesAsync(dbContext);
                    resultResponse.Record = records;
                }
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkUpdateAsync<TEntity>(IEnumerable<TEntity> entities, bool ignoreNulls = false)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                List<TEntity> records = [];
                foreach (var entity in entities)
                {
                    var result = await UpdateAsync(dbContext, entity, ignoreNulls, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;

                    if (result.Record != null)
                        records.Add(result.Record);
                }

                if (resultResponse.Successful)
                {
                    await SaveChangesAsync(dbContext);
                    resultResponse.Record = records;
                }
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkSaveAsync<TEntity>(IEnumerable<TEntity> entities, bool ignoreNullsOnUpdate = false)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                List<TEntity> records = [];
                foreach (var entity in entities)
                {
                    var result = await SaveAsync(dbContext, entity, ignoreNullsOnUpdate, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;

                    if(result.Record != null)
                        records.Add(result.Record);
                }

                if (resultResponse.Successful)
                {
                    await SaveChangesAsync(dbContext);
                    resultResponse.Record = records;
                }
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkDeleteAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var entity in entities)
                {
                    var result = await DeleteAsync(dbContext, entity, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkDeleteAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var primaryKey in primaryKeys)
                {
                    var result = await DeleteAsync<TEntity>(dbContext, primaryKey, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkArchiveAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var entity in entities)
                {
                    var result = await ArchiveAsync(dbContext, entity, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkArchiveAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var primaryKey in primaryKeys)
                {
                    var result = await DeleteAsync<TEntity>(dbContext, primaryKey, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);

            }
            catch(Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkRestoreAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var entity in entities)
                {
                    var result = await RestoreAsync(dbContext, entity, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);
            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public async Task<ResultResponse<IEnumerable<TEntity>>> BulkRestoreAsync<TEntity>(IEnumerable<object?[]?> primaryKeys)
            where TEntity : BaseEntity
        {
            var resultResponse = new ResultResponse<IEnumerable<TEntity>>();
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                foreach (var primaryKey in primaryKeys)
                {
                    var result = await RestoreAsync<TEntity>(dbContext, primaryKey, saveChanges: false);
                    resultResponse.CombineResponse(result.Messages, result.Exception);

                    if (!resultResponse.Successful)
                        break;
                }

                if (resultResponse.Successful)
                    await SaveChangesAsync(dbContext);

            }
            catch (Exception ex)
            {
                resultResponse.Exception = ex;
            }

            return resultResponse;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async void DisposeAsync()
        {
            Dispose();
            await Task.Yield();
        }
    }
}
