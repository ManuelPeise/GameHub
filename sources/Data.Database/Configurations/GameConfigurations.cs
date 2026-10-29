using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Models.Account;
using Shared.Models.Games;

namespace Data.Database.Configurations;

internal sealed class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    /// <summary>Partial unique index: one open singleplayer session per owner and game type.</summary>
    public const string OneOpenSingleplayerSessionIndex = "IX_GameSession_OneOpenSingleplayerPerOwnerAndGame";
    public const string InviteCodeIndex = "IX_GameSession_InviteCode";

    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("GameSession", GameDbContext.GameSchema);

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.GameType).HasMaxLength(32).IsRequired();
        builder.Property(session => session.InviteCode).HasMaxLength(16);
        builder.Property(session => session.Mode).HasConversion<string>().HasMaxLength(16);
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(session => session.StateJson).HasColumnType("jsonb").IsRequired();
        builder.Property(session => session.StateVersion).IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(session => session.Seats)
            .WithOne(seat => seat.Session)
            .HasForeignKey(seat => seat.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(session => new { session.OwnerUserId, session.GameType })
            .HasDatabaseName(OneOpenSingleplayerSessionIndex)
            .IsUnique()
            .HasFilter($"\"Mode\" = '{nameof(GameMode.Singleplayer)}' AND \"Status\" IN ('{nameof(SessionStatus.Running)}', '{nameof(SessionStatus.Paused)}')");

        builder.HasIndex(session => session.InviteCode)
            .HasDatabaseName(InviteCodeIndex)
            .IsUnique()
            .HasFilter("\"InviteCode\" IS NOT NULL");

        // Cleanup job: inactive paused sessions.
        builder.HasIndex(session => new { session.Status, session.LastActivityAt });
    }
}

internal sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public const string HumanOrAiCheck = "CK_Seat_HumanHasUser_AiHasDifficulty";

    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("Seat", GameDbContext.GameSchema, table => table.HasCheckConstraint(
            HumanOrAiCheck,
            $"(\"Kind\" = '{nameof(SeatKind.Human)}' AND \"UserId\" IS NOT NULL AND \"AiDifficulty\" IS NULL AND \"AiStrategyVersion\" IS NULL)"
            + $" OR (\"Kind\" = '{nameof(SeatKind.Ai)}' AND \"UserId\" IS NULL AND \"AiDifficulty\" IS NOT NULL AND \"AiStrategyVersion\" IS NOT NULL)"));

        builder.HasKey(seat => seat.Id);
        builder.Property(seat => seat.Id).ValueGeneratedNever();
        builder.Property(seat => seat.Kind).HasConversion<string>().HasMaxLength(8);
        builder.Property(seat => seat.ConnectionStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(seat => seat.AiDifficulty).HasMaxLength(32);
        builder.Property(seat => seat.AiStrategyVersion).HasMaxLength(32);
        builder.Property(seat => seat.DisplayName).HasMaxLength(AccountRules.DisplayNameMaxLength).IsRequired();
        builder.Property(seat => seat.Symbol).HasMaxLength(16);

        // Accounts are never deleted (only pseudonymized), so the reference stays valid.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(seat => seat.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(seat => new { seat.SessionId, seat.Order }).IsUnique();
        builder.HasIndex(seat => seat.UserId);
    }
}

internal sealed class GameActionConfiguration : IEntityTypeConfiguration<GameAction>
{
    public void Configure(EntityTypeBuilder<GameAction> builder)
    {
        builder.ToTable("GameAction", GameDbContext.GameSchema);

        builder.HasKey(action => action.Id);
        builder.Property(action => action.Id).UseIdentityAlwaysColumn();
        builder.Property(action => action.ActionType).HasMaxLength(64).IsRequired();
        builder.Property(action => action.PayloadJson).HasColumnType("jsonb");
        builder.Property(action => action.RandomResultJson).HasColumnType("jsonb");
        builder.Property(action => action.AiStrategyVersion).HasMaxLength(32);

        builder.HasOne<GameSession>()
            .WithMany()
            .HasForeignKey(action => action.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Seat>()
            .WithMany()
            .HasForeignKey(action => action.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sequence without gaps per session; a second write of the same position fails.
        builder.HasIndex(action => new { action.SessionId, action.Sequence }).IsUnique();
    }
}

internal sealed class GameResultConfiguration : IEntityTypeConfiguration<GameResult>
{
    public void Configure(EntityTypeBuilder<GameResult> builder)
    {
        builder.ToTable("GameResult", GameDbContext.GameSchema);

        builder.HasKey(result => new { result.SessionId, result.SeatId });
        builder.Property(result => result.Outcome).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<GameSession>()
            .WithMany()
            .HasForeignKey(result => result.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Seat>()
            .WithMany()
            .HasForeignKey(result => result.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
