using Microsoft.AspNetCore.Identity;

namespace Data.Database.Entities;

/// <summary>Account of a human player (ASP.NET Core Identity). AI opponents have no account.</summary>
public sealed class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}

public enum UserStatus
{
    Active,
    Locked,

    /// <summary>Personal data removed (pseudonymized); the row stays for game history.</summary>
    Deleted,
}
