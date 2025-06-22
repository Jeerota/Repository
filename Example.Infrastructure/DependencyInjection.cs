using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Interfaces;
using Repository.Implementations;
using Microsoft.EntityFrameworkCore.Internal;

namespace Example.Infrastructure
{
    public static class DependencyInjection
    {
        public static void AddRepository(this IServiceCollection services)
        {
            services.AddTransient<IReadOnlyRepository, SQLRepository<ExampleContext>>();
            services.AddTransient<IRepository, SQLRepository<ExampleContext>>();
        }
    }
}
