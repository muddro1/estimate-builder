using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Customers;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.EstimateBuilder.Components;

public class EstimateHeaderLinkViewComponent : NopViewComponent
{
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public EstimateHeaderLinkViewComponent(IWorkContext workContext, ICustomerService customerService)
    {
        _workContext = workContext;
        _customerService = customerService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (await _customerService.IsGuestAsync(customer))
            return Content(string.Empty);

        return View("~/Plugins/Misc.EstimateBuilder/Views/Components/EstimateHeaderLink/Default.cshtml");
    }
}
