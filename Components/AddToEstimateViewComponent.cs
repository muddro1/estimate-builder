using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Customers;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Plugin.Misc.EstimateBuilder.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.EstimateBuilder.Components;

public class AddToEstimateViewComponent : NopViewComponent
{
    private readonly IEstimateService _estimateService;
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public AddToEstimateViewComponent(
        IEstimateService estimateService,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _estimateService = estimateService;
        _workContext = workContext;
        _customerService = customerService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (await _customerService.IsGuestAsync(customer))
            return Content(string.Empty);

        var productId = 0;
        if (additionalData is ProductDetailsModel.AddToCartModel addToCartModel)
            productId = addToCartModel.ProductId;

        if (productId == 0)
            return Content(string.Empty);

        var estimates = await _estimateService.GetEstimateListsByCustomerIdAsync(customer.Id);

        var model = new AddToEstimateModel
        {
            ProductId = productId,
            Estimates = estimates.Select(e => new EstimateListSelectItem { Id = e.Id, Name = e.Name }).ToList()
        };

        return View("~/Plugins/Misc.EstimateBuilder/Views/Components/AddToEstimate/Default.cshtml", model);
    }
}
