using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Plugin.Misc.EstimateBuilder.Domain;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Plugin.Misc.EstimateBuilder.Services;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class EstimateController : BasePluginController
{
    private const string ListView = EstimateBuilderDefaults.ViewsPath + "/EstimateList.cshtml";
    private const string EditView = EstimateBuilderDefaults.ViewsPath + "/CreateEstimate.cshtml";
    private const string ItemsView = EstimateBuilderDefaults.ViewsPath + "/EstimateItems.cshtml";

    private readonly IEstimateService _estimateService;
    private readonly IEstimateDocumentService _documentService;
    private readonly IEstimateCatalogService _catalogService;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly IProductService _productService;
    private readonly IPictureService _pictureService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IPermissionService _permissionService;
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IDateTimeHelper _dateTimeHelper;
    private readonly INotificationService _notificationService;

    public EstimateController(
        IEstimateService estimateService,
        IEstimateDocumentService documentService,
        IEstimateCatalogService catalogService,
        IWorkContext workContext,
        IStoreContext storeContext,
        IProductService productService,
        IPictureService pictureService,
        IShoppingCartService shoppingCartService,
        IPermissionService permissionService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        IUrlRecordService urlRecordService,
        INopUrlHelper nopUrlHelper,
        IDateTimeHelper dateTimeHelper,
        INotificationService notificationService)
    {
        _estimateService = estimateService;
        _documentService = documentService;
        _catalogService = catalogService;
        _workContext = workContext;
        _storeContext = storeContext;
        _productService = productService;
        _pictureService = pictureService;
        _shoppingCartService = shoppingCartService;
        _permissionService = permissionService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _urlRecordService = urlRecordService;
        _nopUrlHelper = nopUrlHelper;
        _dateTimeHelper = dateTimeHelper;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimates = await _estimateService.GetEstimateListsByCustomerIdAsync(customer.Id);

        var model = new EstimateListsModel();
        foreach (var e in estimates)
            model.Estimates.Add(new EstimateListModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(e.CreatedOnUtc, DateTimeKind.Utc)).ToString("M/d/yyyy")
            });

        return View(ListView, model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(EditView, new EstimateListModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(EstimateListModel model)
    {
        if (!ModelState.IsValid)
            return View(EditView, model);

        var customer = await _workContext.GetCurrentCustomerAsync();
        await _estimateService.InsertEstimateListAsync(new EstimateList
        {
            CustomerId = customer.Id,
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedOnUtc = DateTime.UtcNow
        });

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        return View(EditView, new EstimateListModel
        {
            Id = estimate.Id,
            Name = estimate.Name,
            Description = estimate.Description
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EstimateListModel model)
    {
        if (!ModelState.IsValid)
            return View(EditView, model);

        var estimate = await GetOwnEstimateAsync(model.Id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        estimate.Name = model.Name.Trim();
        estimate.Description = model.Description?.Trim();
        await _estimateService.UpdateEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate != null)
            await _estimateService.DeleteEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Copy(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate != null)
            await _estimateService.CopyEstimateListAsync(id, estimate.CustomerId);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        return View(ItemsView, await BuildEstimateItemsModelAsync(estimate));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateItems(EstimateItemsModel model)
    {
        var estimate = await GetOwnEstimateAsync(model.EstimateListId);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var nameError = EstimateModelHelper.ValidateNameAndDescription(model.EstimateName, model.Description);
        if (nameError != null)
        {
            _notificationService.ErrorNotification(nameError);
            return RedirectToAction(nameof(Details), new { id = estimate.Id });
        }

        estimate.Name = model.EstimateName.Trim();
        estimate.Description = model.Description?.Trim();
        await _estimateService.UpdateEstimateListAsync(estimate);

        await EstimateModelHelper.ApplyItemChangesAsync(_estimateService, estimate.Id, model.Items);

        return RedirectToAction(nameof(Details), new { id = estimate.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var items = await _estimateService.GetItemsByEstimateListIdAsync(id);
        var products = (await _productService.GetProductsByIdsAsync(items.Select(i => i.ProductId).Distinct().ToArray()))
            .ToDictionary(p => p.Id);

        var added = 0;
        var problems = new List<string>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product.Deleted)
            {
                problems.Add($"Product #{item.ProductId} is no longer available.");
                continue;
            }

            // The cart service enforces published/ACL/store/stock/attribute rules and reports why an item was refused
            var warnings = await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, store.Id,
                attributesXml: item.AttributesXml, quantity: item.Quantity);

            if (warnings.Any())
                problems.AddRange(warnings.Select(w => $"{product.Name}: {w}"));
            else
                added++;
        }

        foreach (var problem in problems)
            _notificationService.WarningNotification(problem);

        if (added == 0)
        {
            if (!problems.Any())
                _notificationService.WarningNotification("This estimate has no items to add.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToRoute(NopRouteNames.General.CART);
    }

    [HttpPost]
    public async Task<IActionResult> AddToEstimate(int productId, int estimateListId, int quantity = 1,
        string? newEstimateName = null, string? newEstimateDescription = null)
    {
        if (quantity < 1 || quantity > EstimateBuilderDefaults.MaxQuantity)
            return Json(new { success = false, message = $"Quantity must be between 1 and {EstimateBuilderDefaults.MaxQuantity:N0}." });

        var customer = await _workContext.GetCurrentCustomerAsync();
        var product = await _productService.GetProductByIdAsync(productId);
        if (!await IsProductVisibleAsync(product))
            return Json(new { success = false, message = "Product not found." });

        EstimateList? estimate = null;
        if (estimateListId != 0)
        {
            estimate = await GetOwnEstimateAsync(estimateListId);
            if (estimate == null)
                return Json(new { success = false, message = "Invalid estimate list." });
        }
        else
        {
            var nameError = EstimateModelHelper.ValidateNameAndDescription(newEstimateName, newEstimateDescription);
            if (nameError != null)
                return Json(new { success = false, message = nameError });
        }

        var form = Request.HasFormContentType ? Request.Form : FormCollection.Empty;
        var (attributesXml, errors) = await _catalogService.ParseAndValidateAttributesAsync(product!, form, customer, quantity);
        if (errors.Any())
            return Json(new { success = false, message = string.Join(" ", errors) });

        // Only create the new estimate once the item is known to be valid
        if (estimate == null)
        {
            estimate = new EstimateList
            {
                CustomerId = customer.Id,
                Name = newEstimateName!.Trim(),
                Description = newEstimateDescription?.Trim(),
                CreatedOnUtc = DateTime.UtcNow
            };
            await _estimateService.InsertEstimateListAsync(estimate);
        }

        await _estimateService.AddOrIncrementItemAsync(estimate.Id, product!.Id, quantity, attributesXml);

        return Json(new { success = true, estimateId = estimate.Id, message = $"{product.Name} Product Added Successfully To Estimate" });
    }

    [HttpGet]
    public async Task<IActionResult> GetProductsByCategory(int categoryId)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        return Json(await _catalogService.GetProductOptionsAsync(categoryId, store.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetProductAttributes(int productId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (!await IsProductVisibleAsync(product))
            return Json(new { hasAttributes = false });

        return Json(await _catalogService.GetAttributeOptionsAsync(product!, includePriceAdjustments: false));
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var document = await PrepareDocumentAsync(estimate);
        return File(await _documentService.ExportToExcelAsync(document),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _documentService.GetExportFileName(estimate, "xlsx"));
    }

    [HttpGet]
    public async Task<IActionResult> ExportToPdf(int id)
    {
        var estimate = await GetOwnEstimateAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var document = await PrepareDocumentAsync(estimate);
        return File(await _documentService.ExportToPdfAsync(document), "application/pdf",
            _documentService.GetExportFileName(estimate, "pdf"));
    }

    private async Task<EstimateList?> GetOwnEstimateAsync(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        return estimate != null && estimate.CustomerId == customer.Id ? estimate : null;
    }

    private async Task<bool> IsProductVisibleAsync(Nop.Core.Domain.Catalog.Product? product)
    {
        return product != null && !product.Deleted && product.Published
            && await _aclService.AuthorizeAsync(product)
            && await _storeMappingService.AuthorizeAsync(product);
    }

    private async Task<EstimateDocument> PrepareDocumentAsync(EstimateList estimate)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var showPrices = await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.DISPLAY_PRICES);
        return await _documentService.PrepareAsync(estimate, customer, showPrices);
    }

    private async Task<EstimateItemsModel> BuildEstimateItemsModelAsync(EstimateList estimate)
    {
        var document = await PrepareDocumentAsync(estimate);
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new EstimateItemsModel
        {
            EstimateListId = estimate.Id,
            EstimateName = estimate.Name,
            Description = estimate.Description,
            ShowPrices = document.ShowPrices,
            SubTotalFormatted = document.SubTotalFormatted,
            Categories = await _catalogService.GetCategoryOptionsAsync(store.Id)
        };

        foreach (var line in document.Lines)
            model.Items.Add(await EstimateModelHelper.ToItemModelAsync(line, _pictureService, _urlRecordService, _nopUrlHelper));

        return model;
    }
}
