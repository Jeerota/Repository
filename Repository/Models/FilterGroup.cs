using System.Linq.Expressions;

namespace Repository.Models
{
    public class FilterGroup(List<Filter> filters, ExpressionType expressionType = ExpressionType.AndAlso)
    {
        public ExpressionType ExpressionType { get; set; } = expressionType;
        public List<Filter> Filters { get; set; } = filters;
    }
}
