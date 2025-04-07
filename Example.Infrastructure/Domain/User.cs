using Repository.Models;

namespace Example.Infrastructure
{
    public class User : BaseEntity
    {
        Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
