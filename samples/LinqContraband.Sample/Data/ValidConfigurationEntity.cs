using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinqContraband.Sample.Data
{
    // Valid: Has separate configuration
    public class ValidConfigurationEntity
    {
        public int ConfigKey { get; set; }
    }

    public class ValidConfigurationEntityConfiguration : IEntityTypeConfiguration<ValidConfigurationEntity>
    {
        public void Configure(EntityTypeBuilder<ValidConfigurationEntity> builder)
        {
            builder.HasKey(x => x.ConfigKey);
        }
    }
}

