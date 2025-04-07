using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Interfaces;
using Repository.Implementations;

namespace Repository
{
    public static class DependencyInjection
    {
        public static void AddRepository(this IServiceCollection services)
        {
            services.AddSingleton(typeof(IDbContextFactory<>));
            services.AddScoped<IApplicationContext>();
            services.AddTransient(typeof(IReadOnlyRepository), typeof(SQLRepository<>));
            services.AddTransient(typeof(IRepository), typeof(SQLRepository<>));
        }
    }
}
