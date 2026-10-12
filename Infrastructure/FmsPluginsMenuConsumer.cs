using Nop.Core;
using Nop.Services.Events;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.EstimateBuilder.Infrastructure;

/// <summary>
/// Adds an "Estimate Builder" group (All Estimates, New Estimate, Settings) under the shared "FMS Plugins"
/// admin menu. Every FMS plugin carries a consumer like this and looks the parent up by the same system name,
/// so whichever runs first creates it and the rest add themselves underneath.
/// </summary>
public class FmsPluginsMenuConsumer : IConsumer<AdminMenuCreatedEvent>
{
    // Shared by all FMS plugins - keep identical in every copy
    private const string ParentSystemName = "FmsPlugins";
    private const string ParentTitle = "FMS Plugins";
    private const string ParentIconClass = "fas fa-puzzle-piece";
    private const string InsertBeforeSystemName = "Third party plugins";

    private readonly IPluginManager<IPlugin> _pluginManager;
    private readonly IWorkContext _workContext;

    public FmsPluginsMenuConsumer(IPluginManager<IPlugin> pluginManager, IWorkContext workContext)
    {
        _pluginManager = pluginManager;
        _workContext = workContext;
    }

    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        // Only fully installed plugins load here; the event can fire mid-install
        if (await _pluginManager.LoadPluginBySystemNameAsync(EstimateBuilderDefaults.SystemName) == null)
            return;

        var root = eventMessage.RootMenuItem;
        var parent = root.GetItemBySystemName(ParentSystemName);
        if (parent == null)
        {
            parent = new AdminMenuItem
            {
                SystemName = ParentSystemName,
                Title = ParentTitle,
                IconClass = ParentIconClass,
                Visible = true
            };
            if (!root.InsertBefore(InsertBeforeSystemName, parent))
                root.ChildNodes.Add(parent);
        }

        if (parent.ChildNodes.Any(node => node.SystemName == EstimateBuilderDefaults.MenuGroup))
            return;

        // Estimates span every customer, so vendors (who hold Orders.OrdersView by default) never get these links.
        // nopCommerce hides each item the current admin lacks the permission for, and the group once none remain.
        var isVendor = await _workContext.GetCurrentVendorAsync() != null;

        parent.ChildNodes.Add(new AdminMenuItem
        {
            SystemName = EstimateBuilderDefaults.MenuGroup,
            Title = "Estimate Builder",
            IconClass = "far fa-file-alt",
            Visible = true,
            ChildNodes = new List<AdminMenuItem>
            {
                new()
                {
                    SystemName = EstimateBuilderDefaults.MenuAllEstimates,
                    Title = "All Estimates",
                    IconClass = "far fa-dot-circle",
                    Url = "~/Admin/AdminEstimate/Index",
                    Visible = !isVendor,
                    PermissionNames = new List<string> { StandardPermission.Orders.ORDERS_VIEW }
                },
                new()
                {
                    SystemName = EstimateBuilderDefaults.MenuNewEstimate,
                    Title = "New Estimate",
                    IconClass = "far fa-dot-circle",
                    Url = "~/Admin/AdminEstimate/Create",
                    Visible = !isVendor,
                    PermissionNames = new List<string> { StandardPermission.Orders.ORDERS_CREATE_EDIT_DELETE }
                },
                new()
                {
                    SystemName = EstimateBuilderDefaults.MenuSettings,
                    Title = "Settings",
                    IconClass = "far fa-dot-circle",
                    Url = "~/Admin/AdminEstimate/Configure",
                    Visible = true,
                    PermissionNames = new List<string> { StandardPermission.Configuration.MANAGE_PLUGINS }
                }
            }
        });

        parent.ChildNodes = parent.ChildNodes.OrderBy(node => node.Title).ToList();
    }
}
