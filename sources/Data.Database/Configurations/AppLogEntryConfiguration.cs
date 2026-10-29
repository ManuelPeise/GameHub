using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Database.Configurations;

internal sealed class AppLogEntryConfiguration : IEntityTypeConfiguration<AppLogEntry>
{
    public void Configure(EntityTypeBuilder<AppLogEntry> builder)
    {
        builder.ToTable("AppLog", GameDbContext.LogSchema);

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).UseIdentityAlwaysColumn();
        builder.Property(entry => entry.Level).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.Category).HasMaxLength(256).IsRequired();
        builder.Property(entry => entry.Message).IsRequired();
        builder.Property(entry => entry.Properties).HasColumnType("jsonb");

        // Filtering by time range and level in the admin view; deleting by age.
        builder.HasIndex(entry => entry.Timestamp);
        builder.HasIndex(entry => new { entry.Level, entry.Timestamp });
    }
}
