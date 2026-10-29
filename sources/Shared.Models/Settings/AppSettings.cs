using Microsoft.Extensions.Configuration;
using Shared.Models.Interfaces;

namespace Shared.Models.Settings;

public class AppSettings: IAppSettings
{
    private IConfiguration _configuration;

    public AppSettings(IConfiguration configuration)
    {
        _configuration = configuration;
    }
}
