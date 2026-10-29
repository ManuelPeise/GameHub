using Shared.Models.Interfaces;

namespace Logic.Shared;

/// <summary>
/// Base class for logic classes. Settings are passed as <see cref="IAppSettings"/>;
/// logic classes never read <c>IConfiguration</c> directly (CLAUDE.md rule 7).
/// </summary>
public abstract class ALogicBase
{
    protected ALogicBase(IAppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        AppSettings = appSettings;
    }

    protected IAppSettings AppSettings { get; }
}
