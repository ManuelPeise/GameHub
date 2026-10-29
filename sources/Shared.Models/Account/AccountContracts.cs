namespace Shared.Models.Account;

/// <summary>Request body of POST /api/account/register.</summary>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Response of a registration: always the same text, whether the address is new or not.</summary>
public sealed record RegisterResponse(string Message);

/// <summary>Response of GET /api/account/password-rules: rules as shown to the user.</summary>
public sealed record PasswordRulesResponse(IReadOnlyList<string> Rules);

/// <summary>Request body of POST /api/account/login.</summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe);

/// <summary>The signed-in user (POST /api/account/login, GET /api/account/me).</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

/// <summary>Request body with only an e-mail address (forgot password, resend confirmation).</summary>
public sealed record EmailRequest(string Email);

/// <summary>Request body of POST /api/account/reset-password (values from the link in the mail).</summary>
public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

/// <summary>Response for requests that must not reveal whether an address exists.</summary>
public sealed record MessageResponse(string Message);

/// <summary>Machine-readable reasons of a failed login (extension "code" of the 401 problem response).</summary>
public static class LoginErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string LockedOut = "locked_out";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string AccountDisabled = "account_disabled";
}
