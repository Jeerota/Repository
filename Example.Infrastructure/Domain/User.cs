using Repository.Models;

namespace Example.Infrastructure.Domain
{
    public class User : BaseEntity
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
