using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.EstimateBuilder.Domain;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Plugin.Misc.EstimateBuilder.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class AdminEstimateController : BasePluginController
{
    private const int PageSize = 25;
    private const string ListView = EstimateBuilderDefaults.ViewsPath + "/Admin/AdminEstimateList.cshtml";
    private const string CreateView = EstimateBuilderDefaults.ViewsPath + "/Admin/AdminCreateEstimate.cshtml";
    private const string DetailsView = EstimateBuilderDefaults.ViewsPath + "/Admin/AdminEstimateDetails.cshtml";

    private readonly IEstimateService _estimateService;
    private readonly IEstimateDocumentService _documentService;
    private readonly IEstimateCatalogService _catalogService;
    private readonly IWorkContext _workContext;
    private readonly IProductService _productService;
    private readonly IPictureService _pictureService;
    private readonly IPermissionService _permissionService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly ICustomerService _customerService;
    private readonly IDateTimeHelper _dateTimeHelper;
    private readonly INotificationService _notificationService;

    public AdminEstimateController(
        IEstimateService estimateService,
        IEstimateDocumentService documentService,
        IEstimateCatalogService catalogService,
        IWorkContext workContext,
        IProductService productService,
        IPictureService pictureService,
        IPermissionService permissionService,
        IUrlRecordService urlRecordService,
        INopUrlHelper nopUrlHelper,
        ICustomerService customerService,
        IDateTimeHelper dateTimeHelper,
        INotificationService notificationService)
    {
        _estimateService = estimateService;
        _documentService = documentService;
        _catalogService = catalogService;
        _workContext = workContext;
        _productService = productService;
        _pictureService = pictureService;
        _permissionService = permissionService;
        _urlRecordService = urlRecordService;
        _nopUrlHelper = nopUrlHelper;
        _customerService = customerService;
        _dateTimeHelper = dateTimeHelper;
        _notificationService = notificationService;
    }

    #region Access

    // Estimates span every customer, so vendors (who also hold Orders.OrdersView by default) are always excluded
    private async Task<bool> CanViewAsync() =>
        await _workContext.GetCurrentVendorAsync() == null
        && await _permissionService.AuthorizeAsync(StandardPermission.Orders.ORDERS_VIEW);

    private async Task<bool> CanEditAsync() =>
        await CanViewAsync()
        && await _permissionService.AuthorizeAsync(StandardPermission.Orders.ORDERS_CREATE_EDIT_DELETE);

    #endregion

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm = null, int page = 1)
    {
        if (!await CanViewAsync())
            return AccessDeniedView();

        var all = await _estimateService.GetAllEstimateListsAsync();
        var customerIds = all.Where(e => e.CustomerId > 0).Select(e => e.CustomerId).Distinct().ToArray();
        var customers = (await _customerService.GetCustomersByIdsAsync(customerIds)).ToDictionary(c => c.Id);

        var rows = new List<(EstimateList Estimate, string Owner, string OwnerType)>();
        foreach (var e in all)
        {
            string ownerDisplay, ownerType;
            if (e.CustomerId > 0)
            {
                ownerDisplay = customers.TryGetValue(e.CustomerId, out var customer)
                    ? FormatCustomer(customer)
                    : $"Customer #{e.CustomerId}";
                ownerType = "Customer";
            }
            else
            {
                ownerDisplay = e.ContactName ?? e.ContactEmail ?? "Unknown Contact";
                ownerType = "Contact";
            }

            if (!string.IsNullOrWhiteSpace(searchTerm) &&
                !ownerDisplay.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !e.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !(e.ContactEmail?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;

            rows.Add((e, ownerDisplay, ownerType));
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)PageSize));
        var pageIndex = Math.Clamp(page, 1, totalPages) - 1;
        var pageRows = rows.Skip(pageIndex * PageSize).Take(PageSize).ToList();
        var itemCounts = await _estimateService.GetItemCountsAsync(pageRows.Select(r => r.Estimate.Id).ToList());

        var model = new AdminEstimateListsModel
        {
            SearchTerm = searchTerm,
            PageIndex = pageIndex,
            TotalPages = totalPages,
            TotalCount = rows.Count
        };

        foreach (var (e, owner, ownerType) in pageRows)
            model.Estimates.Add(new AdminEstimateRowModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                OwnerDisplay = owner,
                OwnerType = ownerType,
                ItemCount = itemCounts.TryGetValue(e.Id, out var count) ? count : 0,
                CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(e.CreatedOnUtc, DateTimeKind.Utc)).ToString("M/d/yyyy")
            });

        return View(ListView, model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await CanEditAsync())
            return AccessDeniedView();

        return View(CreateView, new AdminCreateEstimateModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(AdminCreateEstimateModel model)
    {
        if (!await CanEditAsync())
            return AccessDeniedView();

        Customer? customer = null;
        if (model.IsExistingCustomer)
        {
            customer = model.CustomerId > 0 ? await _customerService.GetCustomerByIdAsync(model.CustomerId) : null;
            if (customer == null || customer.Deleted)
                ModelState.AddModelError(nameof(model.CustomerSearch), "Select a customer from the search results.");
        }

        if (!ModelState.IsValid)
            return View(CreateView, model);

        var estimate = new EstimateList
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedOnUtc = DateTime.UtcNow
        };

        if (customer != null)
        {
            estimate.CustomerId = customer.Id;
        }
        else
        {
            estimate.CustomerId = 0;
            estimate.ContactName = model.ContactName?.Trim();
            estimate.ContactEmail = model.ContactEmail?.Trim();
            estimate.ContactPhone = model.ContactPhone?.Trim();
            estimate.ContactAddress = model.ContactAddress?.Trim();
            estimate.ContactCity = model.ContactCity?.Trim();
            estimate.ContactState = model.ContactState?.Trim();
            estimate.ContactZip = model.ContactZip?.Trim();
        }

        await _estimateService.InsertEstimateListAsync(estimate);
        return RedirectToAction(nameof(Details), new { id = estimate.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (!await CanViewAsync())
            return AccessDeniedView();

        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        return View(DetailsView, await BuildAdminDetailsModelAsync(estimate));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateDetails(AdminEstimateDetailsModel model)
    {
        if (!await CanEditAsync())
            return AccessDeniedView();

        var estimate = await _estimateService.GetEstimateListByIdAsync(model.EstimateListId);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var error = EstimateModelHelper.ValidateNameAndDescription(model.EstimateName, model.Description);
        if (error == null && !ModelState.IsValid)
            error = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        if (error != null)
        {
            _notificationService.ErrorNotification(error);
            return RedirectToAction(nameof(Details), new { id = estimate.Id });
        }

        estimate.Name = model.EstimateName.Trim();
        estimate.Description = model.Description?.Trim();
        if (estimate.CustomerId == 0)
        {
            estimate.ContactName = model.ContactName?.Trim();
            estimate.ContactEmail = model.ContactEmail?.Trim();
            estimate.ContactPhone = model.ContactPhone?.Trim();
            estimate.ContactAddress = model.ContactAddress?.Trim();
            estimate.ContactCity = model.ContactCity?.Trim();
            estimate.ContactState = model.ContactState?.Trim();
            estimate.ContactZip = model.ContactZip?.Trim();
        }
        await _estimateService.UpdateEstimateListAsync(estimate);

        await EstimateModelHelper.ApplyItemChangesAsync(_estimateService, estimate.Id, model.Items);

        return RedirectToAction(nameof(Details), new { id = estimate.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AssignCustomer(int estimateId, int customerId)
    {
        if (!await CanEditAsync())
            return Json(new { success = false, message = "Access denied." });

        var estimate = await _estimateService.GetEstimateListByIdAsync(estimateId);
        if (estimate == null)
            return Json(new { success = false, message = "Estimate not found." });

        var customer = await _customerService.GetCustomerByIdAsync(customerId);
        if (customer == null || customer.Deleted || !await _customerService.IsRegisteredAsync(customer))
            return Json(new { success = false, message = "Customer not found." });

        estimate.CustomerId = customerId;
        await _estimateService.UpdateEstimateListAsync(estimate);

        return Json(new { success = true, customerDisplay = FormatCustomer(customer) });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEstimate(int id)
    {
        if (!await CanEditAsync())
            return AccessDeniedView();

        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate != null)
            await _estimateService.DeleteEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> SearchCustomers(string term)
    {
        if (!await CanEditAsync() || !await _permissionService.AuthorizeAsync(StandardPermission.Customers.CUSTOMERS_VIEW))
            return Json(new List<object>());

        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Json(new List<object>());

        var customers = await _customerService.GetAllCustomersAsync(
            email: term.Contains('@') ? term : null,
            firstName: !term.Contains('@') ? term : null,
            pageSize: 15);

        // also try last name if no results
        if (!customers.Any() && !term.Contains('@'))
            customers = await _customerService.GetAllCustomersAsync(lastName: term, pageSize: 15);

        var results = customers
            .Where(c => !c.Deleted && c.Active)
            .Select(c => new
            {
                id = c.Id,
                label = $"{c.FirstName} {c.LastName}".Trim().Length > 0
                    ? $"{c.FirstName} {c.LastName}".Trim() + $" — {c.Email}"
                    : c.Email
            }).ToList();

        return Json(results);
    }

    [HttpPost]
    public async Task<IActionResult> AddToEstimate(int productId, int estimateListId, int quantity = 1)
    {
        if (!await CanEditAsync())
            return Json(new { success = false, message = "Access denied." });

        if (quantity < 1 || quantity > EstimateBuilderDefaults.MaxQuantity)
            return Json(new { success = false, message = $"Quantity must be between 1 and {EstimateBuilderDefaults.MaxQuantity:N0}." });

        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted)
            return Json(new { success = false, message = "Product not found." });

        var estimate = await _estimateService.GetEstimateListByIdAsync(estimateListId);
        if (estimate == null)
            return Json(new { success = false, message = "Invalid estimate list." });

        var pricingCustomer = await _documentService.GetPricingCustomerAsync(estimate);
        var form = Request.HasFormContentType ? Request.Form : FormCollection.Empty;
        var (attributesXml, errors) = await _catalogService.ParseAndValidateAttributesAsync(product, form, pricingCustomer, quantity);
        if (errors.Any())
            return Json(new { success = false, message = string.Join(" ", errors) });

        await _estimateService.AddOrIncrementItemAsync(estimate.Id, product.Id, quantity, attributesXml);

        return Json(new { success = true, message = $"{product.Name} added to estimate." });
    }

    [HttpGet]
    public async Task<IActionResult> GetProductsByCategory(int categoryId, string? q = null)
    {
        if (!await CanViewAsync())
            return Json(new List<object>());

        return Json(await _catalogService.GetProductOptionsAsync(categoryId, q, storeId: 0));
    }

    [HttpGet]
    public async Task<IActionResult> GetProductAttributes(int productId)
    {
        if (!await CanViewAsync())
            return Json(new { hasAttributes = false });

        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted)
            return Json(new { hasAttributes = false });

        return Json(await _catalogService.GetAttributeOptionsAsync(product, includePriceAdjustments: true));
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel(int id)
    {
        if (!await CanViewAsync())
            return AccessDeniedView();

        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
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
        if (!await CanViewAsync())
            return AccessDeniedView();

        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null)
            return RedirectToAction(nameof(Index));

        var document = await PrepareDocumentAsync(estimate);
        return File(await _documentService.ExportToPdfAsync(document), "application/pdf",
            _documentService.GetExportFileName(estimate, "pdf"));
    }

    private static string FormatCustomer(Customer customer)
    {
        var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
        return fullName.Length > 0
            ? $"{fullName} ({customer.Email})"
            : customer.Email ?? $"Customer #{customer.Id}";
    }

    private async Task<EstimateDocument> PrepareDocumentAsync(EstimateList estimate)
    {
        // Price the estimate as its owner would see it, not as the signed-in admin
        var pricingCustomer = await _documentService.GetPricingCustomerAsync(estimate);
        return await _documentService.PrepareAsync(estimate, pricingCustomer, showPrices: true);
    }

    private async Task<AdminEstimateDetailsModel> BuildAdminDetailsModelAsync(EstimateList estimate)
    {
        var document = await PrepareDocumentAsync(estimate);

        var isCustomerEstimate = estimate.CustomerId > 0;
        var customerDisplay = string.Empty;
        if (isCustomerEstimate)
        {
            var customer = await _customerService.GetCustomerByIdAsync(estimate.CustomerId);
            customerDisplay = customer != null ? FormatCustomer(customer) : $"Customer #{estimate.CustomerId}";
        }

        var model = new AdminEstimateDetailsModel
        {
            EstimateListId = estimate.Id,
            EstimateName = estimate.Name,
            Description = estimate.Description,
            ShowPrices = true,
            SubTotalFormatted = document.SubTotalFormatted,
            Categories = await _catalogService.GetCategoryOptionsAsync(storeId: 0),
            IsCustomerEstimate = isCustomerEstimate,
            CustomerId = estimate.CustomerId,
            CustomerDisplay = customerDisplay,
            ContactName = estimate.ContactName,
            ContactEmail = estimate.ContactEmail,
            ContactPhone = estimate.ContactPhone,
            ContactAddress = estimate.ContactAddress,
            ContactCity = estimate.ContactCity,
            ContactState = estimate.ContactState,
            ContactZip = estimate.ContactZip
        };

        foreach (var line in document.Lines)
            model.Items.Add(await EstimateModelHelper.ToItemModelAsync(line, _pictureService, _urlRecordService, _nopUrlHelper));

        return model;
    }
}
