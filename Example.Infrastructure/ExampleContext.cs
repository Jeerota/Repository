using Example.Infrastructure.Configuration;
using Example.Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Example.Infrastructure
{
    public class ExampleContext(DbContextOptions<ExampleContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var configurations = Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.Namespace == typeof(UserConfiguration).Namespace);

            foreach(var configuration in configurations)
            {
                modelBuilder.ApplyConfiguration((dynamic)Activator.CreateInstance(configuration));
            }

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<User> User { get; set; }
        public DbSet<Data> Data { get; set; }
    }
}
