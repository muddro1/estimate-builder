using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.EstimateBuilder.Services;

namespace Nop.Plugin.Misc.EstimateBuilder.Infrastructure;

public class NopStartup : INopStartup
{
    public int Order => 3000;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEstimateService, EstimateService>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }
}
