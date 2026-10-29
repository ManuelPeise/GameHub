namespace Shared.Models.Settings;

/// <summary>Thrown at startup when the "App" configuration is missing values or contains invalid ones.</summary>
public sealed class AppSettingsValidationException : Exception
{
    public AppSettingsValidationException(IReadOnlyList<string> errors)
        : base("Invalid application settings:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, errors))
    {
        Errors = errors;
    }

    /// <summary>One entry per problem, starting with the full configuration key, e.g. "App:Email:Host: ...".</summary>
    public IReadOnlyList<string> Errors { get; }
}
