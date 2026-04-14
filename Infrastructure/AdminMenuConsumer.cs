using Nop.Core;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.EstimateBuilder.Infrastructure;

public class AdminMenuConsumer : BaseAdminMenuCreatedEventConsumer
{
    private readonly IWebHelper _webHelper;

    public AdminMenuConsumer(IPluginManager<IPlugin> pluginManager, IWebHelper webHelper)
        : base(pluginManager)
    {
        _webHelper = webHelper;
    }

    protected override string PluginSystemName => "Misc.EstimateBuilder";

    // No AfterMenuSystemName = append at end of root
    protected override string AfterMenuSystemName => null!;

    // Always allow - we handle auth in the controller
    protected override Task<bool> CheckAccessAsync() => Task.FromResult(true);

    protected override Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin)
    {
        var baseUrl = _webHelper.GetStoreLocation();

        var item = new AdminMenuItem
        {
            SystemName = "EstimateBuilder",
            Title = "Estimate Builder",
            IconClass = "far fa-file-alt",
            Visible = true,
            ChildNodes = new List<AdminMenuItem>
            {
                new AdminMenuItem
                {
                    SystemName = "EstimateBuilder.AllEstimates",
                    Title = "All Estimates",
                    IconClass = "far fa-circle",
                    Url = baseUrl + "Admin/AdminEstimate",
                    Visible = true
                },
                new AdminMenuItem
                {
                    SystemName = "EstimateBuilder.NewEstimate",
                    Title = "New Estimate",
                    IconClass = "far fa-circle",
                    Url = baseUrl + "Admin/AdminEstimate/Create",
                    Visible = true
                }
            }
        };

        return Task.FromResult(item);
    }
}
