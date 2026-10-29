using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Database.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntry", GameDbContext.AuditSchema);

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).UseIdentityAlwaysColumn();
        builder.Property(entry => entry.Action).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Target).HasMaxLength(256);
        builder.Property(entry => entry.Result).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.Details).HasColumnType("jsonb");

        builder.HasIndex(entry => entry.CreatedAt);
        builder.HasIndex(entry => new { entry.UserId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.Action, entry.CreatedAt });
    }
}
