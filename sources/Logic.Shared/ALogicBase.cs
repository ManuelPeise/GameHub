using Microsoft.Extensions.Configuration;

namespace Logic.Shared;

public abstract class ALogicBase
{
    private readonly IConfiguration? _configuration;

    protected ALogicBase()
    {
        
    }

    protected ALogicBase(IConfiguration configuration): base()
    {
        _configuration = configuration;
    }
}
