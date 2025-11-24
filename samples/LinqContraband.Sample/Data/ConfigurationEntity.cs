using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinqContraband.Sample.Data;

// Valid: Has separate configuration
public class ConfigurationEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ConfigurationEntityConfiguration : IEntityTypeConfiguration<ConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<ConfigurationEntity> builder)
    {
        builder.HasKey(x => x.Id);
    }
}
