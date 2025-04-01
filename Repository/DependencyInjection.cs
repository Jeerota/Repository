using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Interfaces;
using Repository.Implementations;

namespace Repository
{
    public class DependencyInjection
    {
        public static void AddRepository(IServiceCollection services)
        {
            services.AddSingleton(typeof(IDbContextFactory<>));
            services.AddScoped<IApplicationContext>();
            services.AddTransient(typeof(IReadOnlyRepository), typeof(SQLRepository<>));
            services.AddTransient(typeof(IRepository), typeof(SQLRepository<>));
        }
    }
}
