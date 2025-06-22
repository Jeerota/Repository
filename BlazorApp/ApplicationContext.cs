using Repository.Interfaces;

namespace BlazorApp
{
    public class ApplicationContext : IApplicationContext
    {
        public Guid UserId { get; set; }
    }
}
