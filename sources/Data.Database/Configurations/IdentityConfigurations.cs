using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Models.Account;

namespace Data.Database.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(AccountRules.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(16);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>Fixed ids so that the seeded roles are stable across migrations.</summary>
    public static readonly Guid PlayerRoleId = new("6f1c2b7e-4a0d-4c55-9d3e-2b8f0a7c1e01");
    public static readonly Guid AdministratorRoleId = new("6f1c2b7e-4a0d-4c55-9d3e-2b8f0a7c1e02");

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasData(
            new Role
            {
                Id = PlayerRoleId,
                Name = RoleNames.Player,
                NormalizedName = RoleNames.Player.ToUpperInvariant(),
                ConcurrencyStamp = "role-player-v1",
            },
            new Role
            {
                Id = AdministratorRoleId,
                Name = RoleNames.Administrator,
                NormalizedName = RoleNames.Administrator.ToUpperInvariant(),
                ConcurrencyStamp = "role-administrator-v1",
            });
    }
}
