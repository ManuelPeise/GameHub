namespace Shared.Models.Account;

/// <summary>Request body of POST /api/account/register.</summary>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Response of a registration: always the same text, whether the address is new or not.</summary>
public sealed record RegisterResponse(string Message);

/// <summary>Response of GET /api/account/password-rules: rules as shown to the user.</summary>
public sealed record PasswordRulesResponse(IReadOnlyList<string> Rules);
