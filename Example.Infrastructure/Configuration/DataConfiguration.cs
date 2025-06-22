using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Example.Infrastructure.Domain;

namespace Example.Infrastructure.Configuration
{
    public class DataConfiguration : IEntityTypeConfiguration<Data>
    {
        public void Configure(EntityTypeBuilder<Data> builder)
        {
            builder.ToTable("Data", "dbo");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).UseIdentityColumn(1, 1);
        }
    }
}
