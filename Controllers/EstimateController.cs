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
    private readonly IUrlRecordService _urlRecordService;
    private readonly INopUrlHelper _nopUrlHelper;

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
        IUrlRecordService urlRecordService,
        INopUrlHelper nopUrlHelper)
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
        _urlRecordService = urlRecordService;
        _nopUrlHelper = nopUrlHelper;
    }

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
    public async Task<IActionResult> AddToEstimate(int productId, int estimateListId, int quantity = 1, string? newEstimateName = null, string? newEstimateDescription = null)
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

        var existing = await _estimateService.GetEstimateListItemAsync(estimateListId, productId);
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
                Quantity = quantity
            });
        }

        return Json(new { success = true, estimateId = estimateListId, message = $"{product.Name} Product Added Successfully To Estimate" });
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var estimate = await _estimateService.GetEstimateListByIdAsync(id);
        if (estimate == null || estimate.CustomerId != customer.Id)
            return RedirectToAction(nameof(Index));

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Estimate");

        ws.Cell(1, 1).Value = estimate.Name;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(3, 1).Value = "SKU";
        ws.Cell(3, 2).Value = "Product";
        ws.Cell(3, 3).Value = "Price";
        ws.Cell(3, 4).Value = "Qty";
        ws.Cell(3, 5).Value = "Total";

        var headerRow = ws.Row(3);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var items = await _estimateService.GetItemsByEstimateListIdAsync(id);
        int row = 4;
        decimal subTotal = 0;

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null) continue;

            var price = product.Price;
            var total = price * item.Quantity;
            subTotal += total;

            ws.Cell(row, 1).Value = product.Sku;
            ws.Cell(row, 2).Value = product.Name;
            ws.Cell(row, 3).Value = price;
            ws.Cell(row, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 4).Value = item.Quantity;
            ws.Cell(row, 5).Value = total;
            ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        ws.Cell(row, 4).Value = "Sub Total";
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = subTotal;
        ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell(row, 5).Style.Font.Bold = true;

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

            table.AddCell(MakeCell(product.Name));
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

            var price = product.Price;
            var total = price * item.Quantity;
            subTotal += total;

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
                TotalFormatted = $"${total:F2}"
            });
        }

        return new EstimateItemsModel
        {
            EstimateListId = estimate.Id,
            EstimateName = estimate.Name,
            Description = estimate.Description,
            Items = itemModels,
            SubTotalFormatted = $"${subTotal:F2}"
        };
    }
}
