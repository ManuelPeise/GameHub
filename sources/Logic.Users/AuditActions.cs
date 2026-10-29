namespace Logic.Users;

/// <summary>Action names written to the audit log by Logic.Users.</summary>
public static class AuditActions
{
    public const string Registered = "Account.Registered";
    public const string RegistrationWithExistingEmail = "Account.RegistrationWithExistingEmail";
    public const string EmailConfirmed = "Account.EmailConfirmed";
    public const string ConfirmationEmailResent = "Account.ConfirmationEmailResent";
    public const string EmailFailed = "Account.EmailFailed";

    public const string LoginSucceeded = "Account.LoginSucceeded";
    public const string LoginFailed = "Account.LoginFailed";
    public const string Logout = "Account.Logout";

    public const string TwoFactorEnabled = "Account.TwoFactorEnabled";
    public const string AccountDeleted = "Account.Deleted";

    public const string PasswordResetRequested = "Account.PasswordResetRequested";
    public const string PasswordReset = "Account.PasswordReset";
}
