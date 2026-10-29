namespace Logic.Users;

/// <summary>Result of a registration attempt.</summary>
public abstract record RegistrationOutcome
{
    private RegistrationOutcome()
    {
    }

    /// <summary>
    /// Registration accepted. Returned for new and for already registered addresses alike, so the
    /// response does not reveal which addresses exist.
    /// </summary>
    public sealed record Accepted : RegistrationOutcome;

    /// <summary>Input is invalid; errors per field (email, displayName, password), in German.</summary>
    public sealed record Invalid(IReadOnlyDictionary<string, string[]> Errors) : RegistrationOutcome;
}
