using Repository.Models;

namespace Example.Infrastructure.Domain
{
    public class Data : BaseEntity
    {
        public int Id { get; set; }
        public string Value { get; set; }
    }
}
