using Nop.Core;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.EstimateBuilder.Infrastructure;

public class AdminMenuConsumer : BaseAdminMenuCreatedEventConsumer
{
    private readonly IPermissionService _permissionService;
    private readonly IWebHelper _webHelper;
    private readonly IWorkContext _workContext;

    public AdminMenuConsumer(IPluginManager<IPlugin> pluginManager,
        IPermissionService permissionService,
        IWebHelper webHelper,
        IWorkContext workContext)
        : base(pluginManager)
    {
        _permissionService = permissionService;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    protected override string PluginSystemName => EstimateBuilderDefaults.SystemName;

    // No AfterMenuSystemName = append at end of root
    protected override string AfterMenuSystemName => null!;

    // Same rule as AdminEstimateController: order viewers only, never vendors
    protected override async Task<bool> CheckAccessAsync() =>
        await _workContext.GetCurrentVendorAsync() == null
        && await _permissionService.AuthorizeAsync(StandardPermission.Orders.ORDERS_VIEW);

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
