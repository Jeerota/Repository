using System.Linq.Expressions;
using System.Reflection;
using Repository.Enums;

namespace Repository.Models
{
    public class Filter(PropertyInfo property, object? value, FilterType type = FilterType.EqualTo, Expression? expression = null, ExpressionType expressionType = ExpressionType.AndAlso)
    {
        public string PropertyName { get; set; } = property.Name;

        public Expression PropertyAccessor { get; set; } = expression ?? GetExpression(property);

        public FilterType Type { get; set; } = type;

        public object? Value { get; set; } = value;

        public ExpressionType ExpressionType { get; set; } = expressionType;

        protected static Expression GetExpression(PropertyInfo property) =>
            Expression.Property(Expression.Parameter(property.DeclaringType!, "i"), property.Name);
    }
}
