namespace Shared.Models.Account;

/// <summary>Rules for accounts, shared by server-side validation (Identity) and the text shown in Web.App.</summary>
public static class AccountRules
{
    public const int PasswordMinLength = 10;
    public const int DisplayNameMinLength = 3;
    public const int DisplayNameMaxLength = 32;

    /// <summary>Display name of a deleted (pseudonymized) account in game history and results.</summary>
    public const string DeletedUserDisplayName = "Gelöschter Spieler";

    /// <summary>Password rules in the language of the UI (German).</summary>
    public static IReadOnlyList<string> PasswordRuleTexts { get; } =
    [
        $"Mindestens {PasswordMinLength} Zeichen",
        "Mindestens ein Großbuchstabe",
        "Mindestens ein Kleinbuchstabe",
        "Mindestens eine Ziffer",
    ];
}

/// <summary>Role names (ASP.NET Core Identity).</summary>
public static class RoleNames
{
    public const string Player = "Player";
    public const string Administrator = "Administrator";
}
