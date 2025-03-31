using Repository.Enums;
using System.Linq.Expressions;

namespace Repository.Helpers
{
    internal static class PredicateBuilder
    {
        internal static Expression<Func<TEntity, bool>> True<TEntity>() { return param => true; }

        internal static Expression<Func<TEntity, bool>> False<TEntity>() { return param => false; }

        internal static Expression<Func<TEntity, bool>> Create<TEntity>(Expression<Func<TEntity, bool>> predicate) { return predicate; }

        internal static Expression<Func<TEntity, bool>> And<TEntity>(this Expression<Func<TEntity, bool>> first, Expression<Func<TEntity, bool>> second)
        {
            return first.Compose(second, Expression.AndAlso);
        }

        internal static Expression<Func<TEntity, bool>> Or<TEntity>(this Expression<Func<TEntity, bool>> first, Expression<Func<TEntity, bool>> second)
        {
            return first.Compose(second, Expression.OrElse);
        }

        internal static Expression<Func<TEntity, bool>> Not<TEntity>(this Expression<Func<TEntity, bool>> expression)
        {
            var neagte = Expression.Negate(expression.Body);
            return Expression.Lambda<Func<TEntity, bool>>(neagte, expression.Parameters);
        }

        private static Expression<TEntity> Compose<TEntity>(this Expression<TEntity> first, Expression<TEntity> second, Func<Expression, Expression, Expression> merge)
        {
            var map = first.Parameters
                .Select((f, i) => new { f, s = second.Parameters[i] })
                .ToDictionary(p => p.f, p => p.s);

            var secondBody = ParameterRebinder.ReplaceParameters(map, second.Body);

            return Expression.Lambda<TEntity>(merge(first.Body, secondBody), first.Parameters);
        }

        internal static Expression FilterBuilder(Expression typeFilter, Expression prop, FilterType filterType, Type propertyType) =>
            filterType switch
            {
                FilterType.EqualTo => Expression.Equal(prop, typeFilter),
                FilterType.NotEqualTo => Expression.NotEqual(prop, typeFilter),
                FilterType.GreaterThan => Expression.GreaterThan(prop, typeFilter),
                FilterType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(prop, typeFilter),
                FilterType.LessThan => Expression.LessThan(prop, typeFilter),
                FilterType.LessThanOrEqual => Expression.LessThanOrEqual(prop, typeFilter),
                FilterType.Contains => Expression.Call(prop, propertyType.GetMethod(nameof(FilterType.Contains), [propertyType])!, typeFilter),
                FilterType.DoesNotContain => Expression.Not(Expression.Call(prop, propertyType.GetMethod(nameof(FilterType.Contains), [propertyType])!, typeFilter)),
                FilterType.StartsWith => Expression.Call(prop, propertyType.GetMethod(nameof(FilterType.StartsWith), [propertyType])!, typeFilter),
                FilterType.EndsWith => Expression.Call(prop, propertyType.GetMethod(nameof(FilterType.EndsWith), [propertyType])!, typeFilter),
                _ => throw new NotImplementedException($"{nameof(filterType)} is not an implemented filter type.")
            };

        private class ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map) : ExpressionVisitor
        {
            protected readonly Dictionary<ParameterExpression, ParameterExpression> Map = map;

            public static Expression ReplaceParameters(Dictionary<ParameterExpression, ParameterExpression> map, Expression expression) =>
                new ParameterRebinder(map).Visit(expression);

            protected override Expression VisitParameter(ParameterExpression parameterExpression)
            {
                if (Map.TryGetValue(parameterExpression, out ParameterExpression? replacement))
                {
                    parameterExpression = replacement;
                }

                return base.VisitParameter(parameterExpression);
            }
        }

        internal class ReplaceParameterVisitor(IEnumerable<ParameterExpression> oldParameters, ParameterExpression newParameter) : ExpressionVisitor
        {
            protected readonly IEnumerable<ParameterExpression> OldParameters = oldParameters;
            protected readonly ParameterExpression NewParameter = newParameter;

            protected override Expression VisitParameter(ParameterExpression node) =>
                OldParameters.Contains(node) ? NewParameter : base.VisitParameter(node);
        }

        internal class ExtractParameterVisitor : ExpressionVisitor
        {
            protected readonly List<ParameterExpression> Parameters = [];

            public IEnumerable<ParameterExpression> ExtractParameters(Expression expression)
            {
                Visit(expression);
                return Parameters;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                Parameters.Add(node);
                return base.VisitParameter(node);
            }
        }
    }
}
