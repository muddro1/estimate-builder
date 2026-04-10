using System.Text.Json;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.EstimateBuilder.Domain;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Plugin.Misc.EstimateBuilder.Services;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class EstimateController : BasePluginController
{
    private readonly IEstimateService _estimateService;
    private readonly IWorkContext _workContext;
    private readonly IProductService _productService;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IPictureService _pictureService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IPermissionService _permissionService;
    private readonly IStoreContext _storeContext;
    private readonly IProductAttributeParser _productAttributeParser;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductAttributeFormatter _productAttributeFormatter;
    private readonly IUrlRecordService _urlRecordService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly ICategoryService _categoryService;

    public EstimateController(
        IEstimateService estimateService,
        IWorkContext workContext,
        IProductService productService,
        IPriceFormatter priceFormatter,
        IPictureService pictureService,
        IShoppingCartService shoppingCartService,
        IPermissionService permissionService,
        IStoreContext storeContext,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IProductAttributeFormatter productAttributeFormatter,
        IUrlRecordService urlRecordService,
        INopUrlHelper nopUrlHelper,
        ICategoryService categoryService)
    {
        _estimateService = estimateService;
        _workContext = workContext;
        _productService = productService;
        _priceFormatter = priceFormatter;
        _pictureService = pictureService;
        _shoppingCartService = shoppingCartService;
        _permissionService = permissionService;
        _storeContext = storeContext;
        _productAttributeParser = productAttributeParser;
        _productAttributeService = productAttributeService;
        _productAttributeFormatter = productAttributeFormatter;
        _urlRecordService = urlRecordService;
        _nopUrlHelper = nopUrlHelper;
        _categoryService = categoryService;
    }

    private record AttributeSelection(int MappingId, string[] Values);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimates = await _estimateService.GetEstimateListsByCustomerIdAsync(customer.Id);

        var model = new EstimateListsModel
        {
            Estimates = estimates.Select(e => new EstimateListModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                CreatedOn = e.CreatedOnUtc.ToLocalTime().ToString("M/d/yyyy")
            }).ToList()
        };

        return View("~/Plugins/Misc.EstimateBuilder/Views/EstimateList.cshtml", model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("~/Plugins/Misc.EstimateBuilder/Views/CreateEstimate.cshtml", new EstimateListModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(EstimateListModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Misc.EstimateBuilder/Views/CreateEstimate.cshtml", model);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimateList = new EstimateList
        {
            CustomerId = customer.Id,
            Name = model.Name,
            Description = model.Description,
            CreatedOnUtc = DateTime.UtcNow
        };

        await _estimateService.InsertEstimateListAsync(estimateList);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        var model = new EstimateListModel
        {
            Id = estimate.Id,
            Name = estimate.Name,
            Description = estimate.Description
        };

        return View("~/Plugins/Misc.EstimateBuilder/Views/CreateEstimate.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EstimateListModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Misc.EstimateBuilder/Views/CreateEstimate.cshtml", model);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(model.Id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        estimate.Name = model.Name;
        estimate.Description = model.Description;
        await _estimateService.UpdateEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate != null && estimate.CustomerId == customer.Id)
            await _estimateService.DeleteEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Copy(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate != null && estimate.CustomerId == customer.Id)
            await _estimateService.CopyEstimateListAsync(id, customer.Id);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        var model = await BuildEstimateItemsModelAsync(estimate);
        return View("~/Plugins/Misc.EstimateBuilder/Views/EstimateItems.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateItems(EstimateItemsModel model)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(model.EstimateListId);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        estimate.Name = model.EstimateName;
        estimate.Description = model.Description;
        await _estimateService.UpdateEstimateListAsync(estimate);

        foreach (var itemModel in model.Items)
        {
            var item = await _estimateService.GetEstimateListByIdAsync(itemModel.Id) as object;
            var dbItem = (await _estimateService.GetItemsByEstimateListIdAsync(estimate.Id))
                .FirstOrDefault(i => i.Id == itemModel.Id);

            if (dbItem == null) continue;

            if (itemModel.Remove)
                await _estimateService.DeleteEstimateListItemAsync(dbItem);
            else
            {
                dbItem.Quantity = Math.Max(1, itemModel.Quantity);
                await _estimateService.UpdateEstimateListItemAsync(dbItem);
            }
        }

        return RedirectToAction(nameof(Details), new { id = model.EstimateListId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEstimateList(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate != null && estimate.CustomerId == customer.Id)
            await _estimateService.DeleteEstimateListAsync(estimate);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        var items = await _estimateService.GetItemsByEstimateListIdAsync(id);
        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null || product.Deleted) continue;

            await _shoppingCartService.AddToCartAsync(
                customer,
                product,
                Nop.Core.Domain.Orders.ShoppingCartType.ShoppingCart,
                store.Id,
                quantity: item.Quantity);
        }

        return RedirectToRoute("ShoppingCart");
    }

    [HttpPost]
    public async Task<IActionResult> AddToEstimate(int productId, int estimateListId, int quantity = 1, string? newEstimateName = null, string? newEstimateDescription = null, string? attributesJson = null)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted)
            return Json(new { success = false, message = "Product not found." });

        // Create new estimate if requested
        if (estimateListId == 0)
        {
            if (string.IsNullOrWhiteSpace(newEstimateName))
                return Json(new { success = false, message = "Estimate name is required." });

            var newEstimate = new EstimateList
            {
                CustomerId = customer.Id,
                Name = newEstimateName.Trim(),
                Description = newEstimateDescription?.Trim(),
                CreatedOnUtc = DateTime.UtcNow
            };
            await _estimateService.InsertEstimateListAsync(newEstimate);
            estimateListId = newEstimate.Id;
        }

        var estimate = await _estimateService.GetEstimateListByIdAsync(estimateListId);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return Json(new { success = false, message = "Invalid estimate list." });

        // Build attribute XML from JSON selection
        string attributesXml = string.Empty;
        if (!string.IsNullOrWhiteSpace(attributesJson))
        {
            try
            {
                var selections = JsonSerializer.Deserialize<AttributeSelection[]>(attributesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (selections != null)
                {
                    foreach (var sel in selections)
                    {
                        var pam = await _productAttributeService.GetProductAttributeMappingByIdAsync(sel.MappingId);
                        if (pam == null) continue;
                        foreach (var val in sel.Values)
                            attributesXml = _productAttributeParser.AddProductAttribute(attributesXml, pam, val);
                    }
                }
            }
            catch { /* ignore malformed JSON */ }
        }

        // Match on both product and attributes so different configurations are separate line items
        var allItems = await _estimateService.GetItemsByEstimateListIdAsync(estimateListId);
        var existing = allItems.FirstOrDefault(i =>
            i.ProductId == productId &&
            string.Equals(i.AttributesXml ?? string.Empty, attributesXml ?? string.Empty, StringComparison.Ordinal));

        if (existing != null)
        {
            existing.Quantity += quantity;
            await _estimateService.UpdateEstimateListItemAsync(existing);
        }
        else
        {
            await _estimateService.InsertEstimateListItemAsync(new EstimateListItem
            {
                EstimateListId = estimateListId,
                ProductId = productId,
                Quantity = quantity,
                AttributesXml = string.IsNullOrEmpty(attributesXml) ? null : attributesXml
            });
        }

        return Json(new { success = true, estimateId = estimateListId, message = $"{product.Name} Product Added Successfully To Estimate" });
    }

    [HttpGet]
    public async Task<IActionResult> GetProductsByCategory(int categoryId)
    {
        var products = await _productService.SearchProductsAsync(
            categoryIds: new List<int> { categoryId },
            pageSize: 500,
            orderBy: Nop.Core.Domain.Catalog.ProductSortingEnum.NameAsc);

        return Json(products.Select(p => new { id = p.Id, name = p.Name }).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> GetProductAttributes(int productId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
            return Json(new { hasAttributes = false });

        var mappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(productId);
        if (!mappings.Any())
            return Json(new { hasAttributes = false });

        var result = new List<object>();
        foreach (var mapping in mappings)
        {
            var pa = await _productAttributeService.GetProductAttributeByIdAsync(mapping.ProductAttributeId);
            var values = await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id);
            result.Add(new
            {
                mappingId = mapping.Id,
                name = pa?.Name ?? "",
                isRequired = mapping.IsRequired,
                controlType = (int)mapping.AttributeControlType,
                defaultValue = mapping.DefaultValue ?? "",
                values = values.Select(v => new
                {
                    id = v.Id,
                    name = v.Name,
                    priceAdjustment = v.PriceAdjustment,
                    priceAdjustmentStr = v.PriceAdjustment != 0
                        ? (v.PriceAdjustment > 0 ? $"+${v.PriceAdjustment:F2}" : $"-${Math.Abs(v.PriceAdjustment):F2}")
                        : "",
                    isPreSelected = v.IsPreSelected,
                    colorSquaresRgb = v.ColorSquaresRgb ?? ""
                }).ToList()
            });
        }

        return Json(new { hasAttributes = true, attributes = result });
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        var store = await _storeContext.GetCurrentStoreAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Estimate");

        // ── Title ──────────────────────────────────────────────────
        ws.Cell(1, 1).Value = "Estimate";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 18;
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ── Metadata ───────────────────────────────────────────────
        ws.Cell(3, 1).Value = "Date :";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 2).Value = DateTime.Now.ToString("MM/dd/yyyy");

        ws.Cell(4, 1).Value = "Estimate Name :";
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 2).Value = estimate.Name;

        ws.Cell(5, 1).Value = "From :";
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 2).Value = store.Name;

        // ── Column headers ─────────────────────────────────────────
        ws.Cell(7, 1).Value = "SKU";
        ws.Cell(7, 2).Value = "Product";
        ws.Cell(7, 3).Value = "Price";
        ws.Cell(7, 4).Value = "Qty";
        ws.Cell(7, 5).Value = "Total";

        var headerRange = ws.Range(7, 1, 7, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(58, 123, 191);

        var items = await _estimateService.GetItemsByEstimateListIdAsync(id);
        int row = 8;
        decimal subTotal = 0;

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null) continue;

            var price = product.Price;
            var total = price * item.Quantity;
            subTotal += total;

            var relativeUrl = await _nopUrlHelper.RouteGenericUrlAsync<Nop.Core.Domain.Catalog.Product>(
                new { SeName = await _urlRecordService.GetSeNameAsync(product) });
            var productUrl = store.Url.TrimEnd('/') + relativeUrl;

            ws.Cell(row, 1).Value = product.Sku;
            ws.Cell(row, 2).Value = product.Name;
            ws.Cell(row, 2).SetHyperlink(new XLHyperlink(productUrl));
            ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromArgb(58, 123, 191);
            ws.Cell(row, 2).Style.Font.Underline = XLFontUnderlineValues.Single;
            ws.Cell(row, 3).Value = price;
            ws.Cell(row, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 4).Value = item.Quantity;
            ws.Cell(row, 5).Value = total;
            ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        ws.Cell(row, 4).Value = "Total Amount :";
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
        ws.Cell(row, 5).Value = subTotal;
        ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{estimate.Name}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportToPdf(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        var store = await _storeContext.GetCurrentStoreAsync();

        using var stream = new MemoryStream();
        var doc = new Document(PageSize.A4, 36, 36, 40, 40);
        PdfWriter.GetInstance(doc, stream);
        doc.Open();

        var titleFont    = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24);
        var metaLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
        var metaValueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        var headerFont   = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
        var cellFont     = FontFactory.GetFont(FontFactory.HELVETICA, 9);

        // ── Title ──────────────────────────────────────────────────
        doc.Add(new Paragraph("Estimate", titleFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 4
        });

        // ── Horizontal rule ────────────────────────────────────────
        var hrColor = new BaseColor(100, 100, 100);
        var hrChunk1 = new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, hrColor, Element.ALIGN_CENTER, -2));
        doc.Add(new Paragraph(2f, hrChunk1) { SpacingAfter = 2 });

        // ── Metadata ───────────────────────────────────────────────
        var metaTable = new PdfPTable(2) { WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_LEFT, SpacingBefore = 4, SpacingAfter = 4 };
        metaTable.SetWidths(new float[] { 35, 65 });

        void AddMetaRow(string label, string value)
        {
            metaTable.AddCell(new PdfPCell(new Phrase(label, metaLabelFont)) { Border = PdfPCell.NO_BORDER, Padding = 2 });
            metaTable.AddCell(new PdfPCell(new Phrase(value, metaValueFont))  { Border = PdfPCell.NO_BORDER, Padding = 2 });
        }

        AddMetaRow("Date :",          DateTime.Now.ToString("MM/dd/yyyy"));
        AddMetaRow("Estimate Name :", estimate.Name);
        AddMetaRow("From :",          store.Name);

        doc.Add(metaTable);

        // ── Second horizontal rule ─────────────────────────────────
        var hrChunk2 = new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, hrColor, Element.ALIGN_CENTER, -2));
        doc.Add(new Paragraph(2f, hrChunk2) { SpacingAfter = 14 });

        // ── Product table ──────────────────────────────────────────
        // Columns: SKU | Image | Product(s) | Price | Qty. | Total
        var table = new PdfPTable(6) { WidthPercentage = 100, SpacingBefore = 0 };
        table.SetWidths(new float[] { 12, 13, 36, 14, 8, 14 });

        var headerBg = new BaseColor(58, 123, 191);   // match the blue used in the UI
        var white = new BaseColor(255, 255, 255);
        var headerFontWhite = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, white);

        void AddHeaderCell(string text, int align = Element.ALIGN_CENTER)
        {
            table.AddCell(new PdfPCell(new Phrase(text, headerFontWhite))
            {
                BackgroundColor = headerBg,
                Padding = 6,
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE
            });
        }

        AddHeaderCell("SKU");
        AddHeaderCell("Image");
        AddHeaderCell("Product(s)", Element.ALIGN_LEFT);
        AddHeaderCell("Price",  Element.ALIGN_RIGHT);
        AddHeaderCell("Qty.",   Element.ALIGN_CENTER);
        AddHeaderCell("Total",  Element.ALIGN_RIGHT);

        var items = await _estimateService.GetItemsByEstimateListIdAsync(id);
        decimal subTotal = 0;
        bool altRow = false;
        var altBg = new BaseColor(245, 248, 252);

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null) continue;

            var price = product.Price;
            var total = price * item.Quantity;
            subTotal += total;
            var rowBg = altRow ? altBg : white;
            altRow = !altRow;

            PdfPCell MakeCell(string text, int align = Element.ALIGN_LEFT)
                => new PdfPCell(new Phrase(text, cellFont))
                {
                    Padding = 5,
                    HorizontalAlignment = align,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    BackgroundColor = rowBg
                };

            table.AddCell(MakeCell(product.Sku ?? ""));

            // Image cell
            var imgCell = new PdfPCell
            {
                Padding = 4,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                BackgroundColor = rowBg,
                FixedHeight = 70
            };
            var pictures = await _pictureService.GetPicturesByProductIdAsync(product.Id, 1);
            if (pictures.Any())
            {
                var picture = pictures.First();
                var picBytes = await _pictureService.LoadPictureBinaryAsync(picture);
                if (picBytes != null && picBytes.Length > 0)
                {
                    try
                    {
                        var img = iTextSharp.text.Image.GetInstance(picBytes);
                        img.ScaleToFit(60f, 60f);
                        imgCell.AddElement(img);
                    }
                    catch { /* leave empty on image error */ }
                }
            }
            table.AddCell(imgCell);

            // Product name as clickable link
            var relPdfUrl = await _nopUrlHelper.RouteGenericUrlAsync<Nop.Core.Domain.Catalog.Product>(
                new { SeName = await _urlRecordService.GetSeNameAsync(product) });
            var absPdfUrl = store.Url.TrimEnd('/') + relPdfUrl;
            var linkFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, iTextSharp.text.Font.UNDERLINE, new BaseColor(58, 123, 191));
            var anchor = new Anchor(product.Name, linkFont) { Reference = absPdfUrl };
            table.AddCell(new PdfPCell(anchor)
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                BackgroundColor = rowBg
            });
            table.AddCell(MakeCell($"${price:F2}", Element.ALIGN_RIGHT));
            table.AddCell(MakeCell(item.Quantity.ToString(), Element.ALIGN_CENTER));
            table.AddCell(MakeCell($"${total:F2}", Element.ALIGN_RIGHT));
        }

        // ── Total Amount footer row ────────────────────────────────
        var footerBg = new BaseColor(230, 230, 230);
        table.AddCell(new PdfPCell(new Phrase("Total Amount :", metaLabelFont))
        {
            Colspan = 5,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            Padding = 7,
            BackgroundColor = footerBg
        });
        table.AddCell(new PdfPCell(new Phrase($"${subTotal:F2}", metaLabelFont))
        {
            HorizontalAlignment = Element.ALIGN_RIGHT,
            Padding = 7,
            BackgroundColor = footerBg
        });

        doc.Add(table);
        doc.Close();

        return File(stream.ToArray(), "application/pdf", $"{estimate.Name}.pdf");
    }

    private async Task<EstimateItemsModel> BuildEstimateItemsModelAsync(EstimateList estimate)
    {
        var items = await _estimateService.GetItemsByEstimateListIdAsync(estimate.Id);
        var itemModels = new List<EstimateItemModel>();
        decimal subTotal = 0;

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null) continue;

            // Base price + attribute price adjustments
            var price = product.Price;
            if (!string.IsNullOrEmpty(item.AttributesXml))
            {
                var attrValues = await _productAttributeParser.ParseProductAttributeValuesAsync(item.AttributesXml);
                foreach (var av in attrValues)
                    price += av.PriceAdjustmentUsePercentage
                        ? product.Price * av.PriceAdjustment / 100m
                        : av.PriceAdjustment;
            }

            var total = price * item.Quantity;
            subTotal += total;

            // Format selected attributes for display
            string attributesFormatted = string.Empty;
            if (!string.IsNullOrEmpty(item.AttributesXml))
            {
                var customer = await _workContext.GetCurrentCustomerAsync();
                var store = await _storeContext.GetCurrentStoreAsync();
                attributesFormatted = await _productAttributeFormatter.FormatAttributesAsync(
                    product, item.AttributesXml, customer, store,
                    separator: ", ", htmlEncode: false, renderPrices: false);
            }

            string imageUrl = string.Empty;
            var pictures = await _pictureService.GetPicturesByProductIdAsync(product.Id, 1);
            if (pictures.Any())
                imageUrl = await _pictureService.GetPictureUrlAsync(pictures.First().Id, 75);

            itemModels.Add(new EstimateItemModel
            {
                Id = item.Id,
                ProductId = product.Id,
                Sku = product.Sku ?? string.Empty,
                ProductName = product.Name,
                ProductUrl = await _nopUrlHelper.RouteGenericUrlAsync<Nop.Core.Domain.Catalog.Product>(new { SeName = await _urlRecordService.GetSeNameAsync(product) }),
                ImageUrl = imageUrl,
                Price = price,
                PriceFormatted = $"${price:F2}",
                Quantity = item.Quantity,
                Total = total,
                TotalFormatted = $"${total:F2}",
                AttributesFormatted = attributesFormatted
            });
        }

        var allCategories = (await _categoryService.GetAllCategoriesAsync()).ToList();
        var categoryItems = new List<CategorySelectItem>();
        foreach (var cat in allCategories)
        {
            var breadcrumb = await _categoryService.GetFormattedBreadCrumbAsync(cat, allCategories);
            categoryItems.Add(new CategorySelectItem { Id = cat.Id, Name = breadcrumb });
        }

        return new EstimateItemsModel
        {
            EstimateListId = estimate.Id,
            EstimateName = estimate.Name,
            Description = estimate.Description,
            Items = itemModels,
            SubTotalFormatted = $"${subTotal:F2}",
            Categories = categoryItems.OrderBy(c => c.Name).ToList()
        };
    }
}
