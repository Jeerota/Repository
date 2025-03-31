using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Repository.Enums;
using Repository.Helpers;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Repository.Models
{
    public class LookupRequest<TDTO, TEntity>(List<FilterGroup>? filterGroups = null,
        Expression<Func<TEntity, TDTO>>? select = null,
        List<string>? include = null,
        OrderedDictionary<string, ListSortDirection>? orderBy = null,
        List<string>? groupBy = null,
        DateTime? temporalAsOf = null,
        bool includeArchived = false,
        int page = 1,
        int? pageSize = null)
        where TEntity : BaseEntity
    {
        public List<FilterGroup> FilterBy { get; set; } = filterGroups ?? [];

        public Expression<Func<TEntity, TDTO>> Select { get; set; } = select ?? SelfSelect();

        public List<string> Include { get; set; } = include ?? [];

        public OrderedDictionary<string, ListSortDirection> OrderBy { get; set; } = orderBy ?? [];

        public List<string> GroupBy { get; set; } = groupBy ?? [];

        public DateTime? TemporalAsOf { get; set; } = temporalAsOf;

        public bool IncludeArchived { get; set; } = includeArchived; //ToDo: Account for IncludeArchived.

        public int Page { get; set; } = page;

        public int? PageSize { get; set; } = pageSize;

        internal Expression<Func<TEntity, bool>> GetPredicate()
        {
            Expression<Func<TEntity, bool>>? predicate = null;
            var properties = typeof(TEntity).GetProperties();

            if (properties.Length == 0)
                throw new Exception($"No properites found on {typeof(TEntity).Name}");

            var item = Expression.Parameter(typeof(TEntity), "i");
            foreach(var filterGroup in FilterBy)
            {
                Expression<Func<TEntity, bool>>? groupLambda = null;
                foreach(var filter in filterGroup.Filters)
                {
                    Expression<Func<TEntity, bool>>? lambda = null;
                    Type propertyType = typeof(TEntity).GetProperty(filter.PropertyName)?.PropertyType!;
                    var prop = filter.PropertyAccessor;
                    var originalParameters = new PredicateBuilder.ExtractParameterVisitor().ExtractParameters(prop)
                        .Where(x => x.Type == typeof(TEntity));
                    var visitor = new PredicateBuilder.ReplaceParameterVisitor(originalParameters, item);
                    prop = visitor.Visit(prop);

                    if (filter.Value == null)
                        continue;

                    if (filter.Type == Enums.FilterType.In || filter.Type == Enums.FilterType.NotIn)
                    {
                        if(filter.Value is IEnumerable values)
                        {
                            foreach(var filterValue in values)
                            {
                                var filterLambda = GetFilterLamba(item, propertyType, prop, filterValue, filter.Type);

                                if (lambda == null)
                                    lambda = filterLambda;
                                else if (filter.Type == FilterType.In)
                                    lambda = lambda.Or(filterLambda);
                                else if (filter.Type == FilterType.NotIn)
                                    lambda = lambda.And(filterLambda);
                            } 
                        }
                        else
                        {
                            var filterLambda = GetFilterLamba(item, propertyType, prop, filter.Value, filter.Type);

                            if (lambda == null)
                                lambda = filterLambda;
                            else if (filter.Type == FilterType.In)
                                lambda = lambda.Or(filterLambda);
                            else if (filter.Type == FilterType.NotIn)
                                lambda = lambda.And(filterLambda);
                        }
                    }
                    else
                        lambda = GetFilterLamba(item, propertyType, prop, filter.Value, filter.Type);

                    CombinePredicate(groupLambda, lambda, filterGroup.ExpressionType);
                }
                CombinePredicate(predicate, groupLambda, filterGroup.ExpressionType);
            }

            predicate ??= PredicateBuilder.True<TEntity>();

            return predicate;
        }

        private static void CombinePredicate(Expression<Func<TEntity, bool>>? current, Expression<Func<TEntity, bool>>? next, ExpressionType expressionType)
        {
            next ??= PredicateBuilder.True<TEntity>();

            if (current == null)
                current = next;
            else if (expressionType == ExpressionType.AndAlso)
                current.And(next);
            else if (expressionType == ExpressionType.OrElse)
                current.Or(next);
            else
                throw new NotImplementedException($"{expressionType} has not been implemented as a join type.");
        }

        private static Expression<Func<TEntity, bool>> GetFilterLamba(ParameterExpression item, Type propertyType, Expression prop, object value, FilterType type)
        {
            ConstantExpression filterExpression = Expression.Constant(Convert.ChangeType(value, propertyType!.IsGenericType
                ? propertyType.GenericTypeArguments[0]
                : propertyType));
            Expression typeFilter = Expression.Convert(filterExpression, propertyType);
            Expression call = PredicateBuilder.FilterBuilder(typeFilter, prop, (type == FilterType.In
                ? FilterType.EqualTo
                : type == FilterType.NotIn
                    ? FilterType.NotEqualTo
                    : type),
                propertyType);

            return Expression.Lambda<Func<TEntity, bool>>(call, item);
        }

        private static Expression<Func<TEntity, TDTO>> SelfSelect()
        {
            throw new NotImplementedException(); //ToDo: Implement Self Referencing Entity
        }

        internal Expression<Func<TEntity, object>>[] GetInclude()
        {
            List<Expression<Func<TEntity, object>>> includes = [];
            var properties = typeof(TEntity).GetProperties();
            var parameter = Expression.Parameter(typeof(TEntity), "i");

            foreach(var include in Include)
            {
                var propertyInfo = properties.SingleOrDefault(x => x.Name == include) 
                    ?? throw new Exception($"Unable to find property {include} on {typeof(TEntity).Name}.");
                var property = Expression.Property(parameter, include);
                includes.Add(Expression.Lambda<Func<TEntity, object>>(property, parameter));
            }

            return [.. includes];
        }

        internal Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>> GetOrderBy()
        {
            var typeQueryable = typeof(IQueryable<TDTO>);
            var argumentQueryable = Expression.Parameter(typeQueryable, "p");
            var outerExpression = Expression.Lambda(argumentQueryable, argumentQueryable);

            var index = 0;
            foreach(var column in OrderBy.Keys)
            {
                outerExpression = GenerateOrderBy(argumentQueryable,
                    outerExpression,
                    index == 0
                        ? OrderBy[column] == ListSortDirection.Ascending ? "OrderBy" : "OrderByDescending"
                        : OrderBy[column] == ListSortDirection.Ascending ? "ThenBy" : "ThenByDescending",
                    column);
            }

            return (Func<IQueryable<TDTO>, IOrderedQueryable<TDTO>>)outerExpression.Compile();
        }

        private static LambdaExpression GenerateOrderBy(ParameterExpression argumentQueryable, LambdaExpression expression, string methodName, string column)
        {
            var argument = Expression.Parameter(typeof(TDTO), "x");

            var property = typeof(TDTO).GetProperty(column, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentNullException(nameof(column));

            var propertyAccess = Expression.Property(argument, property);
            var propertyType = property.PropertyType;
            var orderByLambda = Expression.Lambda(propertyAccess, argument);

            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                [typeof(TEntity), propertyType],
                expression.Body,
                Expression.Quote(orderByLambda)
            );

            return Expression.Lambda(resultExpression, argumentQueryable);
        }

        internal Expression<Func<TDTO, TKey>> GetGroupBy<TKey>()
        {
            throw new NotImplementedException(); //ToDo: Implement Group
        }
    }
}
