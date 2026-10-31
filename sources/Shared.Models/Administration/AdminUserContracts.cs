namespace Shared.Models.Administration;

/// <summary>Status of an account as shown in the admin area.</summary>
public enum AccountStatus
{
    Active,
    Locked,

    /// <summary>Pseudonymized; only kept for the game history.</summary>
    Deleted,
}

/// <summary>A player in the admin list.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string DisplayName,
    string Email,
    AccountStatus Status,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsMe);

/// <summary>One page of the admin list with the total number of matches.</summary>
public sealed record AdminUserPageDto(IReadOnlyList<AdminUserDto> Items, int Total);

/// <summary>Deleting a player has to be confirmed explicitly.</summary>
public sealed record AdminDeleteUserRequest(bool Confirmed);
