namespace Logic.Users;

/// <summary>Action names written to the audit log by Logic.Users.</summary>
public static class AuditActions
{
    public const string Registered = "Account.Registered";
    public const string RegistrationWithExistingEmail = "Account.RegistrationWithExistingEmail";
    public const string EmailConfirmed = "Account.EmailConfirmed";
    public const string ConfirmationEmailFailed = "Account.ConfirmationEmailFailed";
}
