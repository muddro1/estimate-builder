using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using Nop.Plugin.Misc.EstimateBuilder.Components;

namespace Nop.Plugin.Misc.EstimateBuilder;

public class EstimateBuilderPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    public EstimateBuilderPlugin(ISettingService settingService, IWebHelper webHelper)
    {
        _settingService = settingService;
        _webHelper = webHelper;
    }

    public bool HideInWidgetList => false;

    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (widgetZone == PublicWidgetZones.HeaderLinksAfter)
            return typeof(EstimateHeaderLinkViewComponent);

        return typeof(AddToEstimateViewComponent);
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.ProductDetailsAddInfo,
            PublicWidgetZones.HeaderLinksAfter
        });
    }

    public override string GetConfigurationPageUrl()
    {
        return _webHelper.GetStoreLocation() + "Admin/AdminEstimate/Configure";
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<EstimateBuilderSettings>();
        await base.UninstallAsync();
    }
}
