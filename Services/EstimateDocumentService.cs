using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Plugin.Misc.EstimateBuilder.Domain;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Services.Tax;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public class EstimateDocumentService : IEstimateDocumentService
{
    private static readonly BaseColor Accent = new(58, 123, 191);

    private readonly IEstimateService _estimateService;
    private readonly IProductService _productService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly ITaxService _taxService;
    private readonly ICurrencyService _currencyService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IProductAttributeFormatter _productAttributeFormatter;
    private readonly ICustomerService _customerService;
    private readonly ILocalizationService _localizationService;
    private readonly IPictureService _pictureService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IDateTimeHelper _dateTimeHelper;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public EstimateDocumentService(
        IEstimateService estimateService,
        IProductService productService,
        IShoppingCartService shoppingCartService,
        ITaxService taxService,
        ICurrencyService currencyService,
        IPriceCalculationService priceCalculationService,
        IPriceFormatter priceFormatter,
        IProductAttributeFormatter productAttributeFormatter,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IPictureService pictureService,
        IUrlRecordService urlRecordService,
        INopUrlHelper nopUrlHelper,
        IDateTimeHelper dateTimeHelper,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _estimateService = estimateService;
        _productService = productService;
        _shoppingCartService = shoppingCartService;
        _taxService = taxService;
        _currencyService = currencyService;
        _priceCalculationService = priceCalculationService;
        _priceFormatter = priceFormatter;
        _productAttributeFormatter = productAttributeFormatter;
        _customerService = customerService;
        _localizationService = localizationService;
        _pictureService = pictureService;
        _urlRecordService = urlRecordService;
        _nopUrlHelper = nopUrlHelper;
        _dateTimeHelper = dateTimeHelper;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<Customer> GetPricingCustomerAsync(EstimateList estimate)
    {
        if (estimate.CustomerId > 0)
        {
            var owner = await _customerService.GetCustomerByIdAsync(estimate.CustomerId);
            if (owner != null && !owner.Deleted)
                return owner;
        }

        // Contact-only estimates get the prices an anonymous visitor would see
        return await _customerService.GetOrCreateBackgroundTaskUserAsync();
    }

    public async Task<EstimateDocument> PrepareAsync(EstimateList estimate, Customer pricingCustomer, bool showPrices)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var callForPrice = await _localizationService.GetResourceAsync("Products.CallForPrice");
        // The owner's tax display preference, not the signed-in user's (which is what the 3-argument
        // ITaxService.GetProductPriceAsync overload would use)
        var includingTax = await _customerService.GetCustomerTaxDisplayTypeAsync(pricingCustomer) == TaxDisplayType.IncludingTax;

        var items = await _estimateService.GetItemsByEstimateListIdAsync(estimate.Id);
        var products = (await _productService.GetProductsByIdsAsync(items.Select(i => i.ProductId).Distinct().ToArray()))
            .ToDictionary(p => p.Id);

        var lines = new List<EstimateLine>();
        decimal subTotal = 0;

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product.Deleted)
                continue;

            var attributesFormatted = string.IsNullOrEmpty(item.AttributesXml)
                ? string.Empty
                : await _productAttributeFormatter.FormatAttributesAsync(product, item.AttributesXml, pricingCustomer, store,
                    separator: ", ", htmlEncode: false, renderPrices: false);

            var priceAvailable = showPrices && !product.CallForPrice;
            decimal unitPrice = 0, total = 0;
            string unitPriceFormatted = string.Empty, totalFormatted = string.Empty;

            if (priceAvailable)
            {
                var (basePrice, _, _) = await _shoppingCartService.GetUnitPriceAsync(product, pricingCustomer, store,
                    ShoppingCartType.ShoppingCart, item.Quantity, item.AttributesXml ?? string.Empty,
                    decimal.Zero, null, null, true);
                var (taxedPrice, _) = await _taxService.GetProductPriceAsync(product, basePrice, includingTax, pricingCustomer);
                unitPrice = await _priceCalculationService.RoundPriceAsync(
                    await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(taxedPrice, currency), currency);
                total = unitPrice * item.Quantity;
                subTotal += total;

                unitPriceFormatted = await _priceFormatter.FormatPriceAsync(unitPrice, true, currency);
                totalFormatted = await _priceFormatter.FormatPriceAsync(total, true, currency);
            }
            else if (showPrices)
            {
                unitPriceFormatted = callForPrice;
            }

            lines.Add(new EstimateLine(item, product, unitPrice, total, unitPriceFormatted, totalFormatted,
                attributesFormatted, priceAvailable));
        }

        var subTotalFormatted = showPrices ? await _priceFormatter.FormatPriceAsync(subTotal, true, currency) : string.Empty;

        return new EstimateDocument(estimate, lines, subTotal, subTotalFormatted, currency.CurrencyCode, showPrices);
    }

    public async Task<byte[]> ExportToExcelAsync(EstimateDocument document)
    {
        var estimate = document.Estimate;
        var store = await _storeContext.GetCurrentStoreAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Estimate");

        ws.Cell(1, 1).Value = "Estimate";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 18;
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var metaRow = 3;
        foreach (var (label, value) in await GetMetadataAsync(estimate))
        {
            ws.Cell(metaRow, 1).Value = label;
            ws.Cell(metaRow, 1).Style.Font.Bold = true;
            ws.Cell(metaRow, 2).Value = value;
            metaRow++;
        }

        var headerRow = metaRow + 1;
        var priceSuffix = $" ({document.CurrencyCode})";
        ws.Cell(headerRow, 1).Value = "SKU";
        ws.Cell(headerRow, 2).Value = "Product";
        ws.Cell(headerRow, 3).Value = "Price" + priceSuffix;
        ws.Cell(headerRow, 4).Value = "Qty";
        ws.Cell(headerRow, 5).Value = "Total" + priceSuffix;

        var headerRange = ws.Range(headerRow, 1, headerRow, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(58, 123, 191);

        const string moneyFormat = "#,##0.00";
        var row = headerRow + 1;
        foreach (var line in document.Lines)
        {
            ws.Cell(row, 1).Value = line.Product.Sku;
            ws.Cell(row, 2).Value = string.IsNullOrEmpty(line.AttributesFormatted)
                ? line.Product.Name
                : $"{line.Product.Name} ({line.AttributesFormatted})";
            ws.Cell(row, 2).SetHyperlink(new XLHyperlink(await GetProductUrlAsync(line.Product, store.Url)));
            ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromArgb(58, 123, 191);
            ws.Cell(row, 2).Style.Font.Underline = XLFontUnderlineValues.Single;
            ws.Cell(row, 4).Value = line.Item.Quantity;

            if (line.PriceAvailable)
            {
                ws.Cell(row, 3).Value = line.UnitPrice;
                ws.Cell(row, 3).Style.NumberFormat.Format = moneyFormat;
                ws.Cell(row, 5).Value = line.Total;
                ws.Cell(row, 5).Style.NumberFormat.Format = moneyFormat;
            }
            else
            {
                ws.Cell(row, 3).Value = line.UnitPriceFormatted;
            }

            row++;
        }

        if (document.ShowPrices)
        {
            ws.Cell(row, 4).Value = "Total Amount :";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
            ws.Cell(row, 5).Value = document.SubTotal;
            ws.Cell(row, 5).Style.NumberFormat.Format = moneyFormat;
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
        }

        // Size columns before adding the notes block, so a long note doesn't stretch column A
        ws.Columns().AdjustToContents();

        if (!string.IsNullOrWhiteSpace(estimate.Note))
        {
            var noteRow = row + 2;
            ws.Cell(noteRow, 1).Value = "Notes :";
            ws.Cell(noteRow, 1).Style.Font.Bold = true;

            var noteRange = ws.Range(noteRow + 1, 1, noteRow + 1, 5);
            noteRange.Merge();
            noteRange.Value = estimate.Note.Replace("\r\n", "\n");
            noteRange.Style.Alignment.WrapText = true;
            noteRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            // Merged cells don't auto-fit their height; estimate it from line breaks and wrapping
            var totalWidth = Enumerable.Range(1, 5).Sum(c => ws.Column(c).Width);
            var lines = estimate.Note.Replace("\r\n", "\n").Split('\n')
                .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / Math.Max(1, totalWidth))));
            ws.Row(noteRow + 1).Height = Math.Min(409, lines * 15 + 4);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportToPdfAsync(EstimateDocument document)
    {
        var estimate = document.Estimate;
        var store = await _storeContext.GetCurrentStoreAsync();

        using var stream = new MemoryStream();
        var doc = new Document(PageSize.A4, 36, 36, 40, 40);
        PdfWriter.GetInstance(doc, stream);
        doc.Open();

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24);
        var metaLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
        var metaValueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
        var attributeFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, iTextSharp.text.Font.NORMAL, new BaseColor(100, 100, 100));
        var linkFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, iTextSharp.text.Font.UNDERLINE, Accent);

        doc.Add(new Paragraph("Estimate", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 4 });

        var hrColor = new BaseColor(100, 100, 100);
        doc.Add(new Paragraph(2f, new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, hrColor, Element.ALIGN_CENTER, -2))) { SpacingAfter = 2 });

        var metaTable = new PdfPTable(2) { WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_LEFT, SpacingBefore = 4, SpacingAfter = 4 };
        metaTable.SetWidths(new float[] { 35, 65 });
        foreach (var (label, value) in await GetMetadataAsync(estimate))
        {
            metaTable.AddCell(new PdfPCell(new Phrase(label, metaLabelFont)) { Border = PdfPCell.NO_BORDER, Padding = 2 });
            metaTable.AddCell(new PdfPCell(new Phrase(value, metaValueFont)) { Border = PdfPCell.NO_BORDER, Padding = 2 });
        }
        doc.Add(metaTable);

        doc.Add(new Paragraph(2f, new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, hrColor, Element.ALIGN_CENTER, -2))) { SpacingAfter = 14 });

        // Columns: SKU | Image | Product(s) | Price | Qty. | Total
        var table = new PdfPTable(6) { WidthPercentage = 100, SpacingBefore = 0 };
        table.SetWidths(new float[] { 12, 13, 36, 14, 8, 14 });

        var white = new BaseColor(255, 255, 255);
        var headerFontWhite = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, white);
        void AddHeaderCell(string text, int align = Element.ALIGN_CENTER) =>
            table.AddCell(new PdfPCell(new Phrase(text, headerFontWhite)) { BackgroundColor = Accent, Padding = 6, HorizontalAlignment = align, VerticalAlignment = Element.ALIGN_MIDDLE });

        AddHeaderCell("SKU");
        AddHeaderCell("Image");
        AddHeaderCell("Product(s)", Element.ALIGN_LEFT);
        AddHeaderCell("Price", Element.ALIGN_RIGHT);
        AddHeaderCell("Qty.", Element.ALIGN_CENTER);
        AddHeaderCell("Total", Element.ALIGN_RIGHT);

        var altRow = false;
        var altBg = new BaseColor(245, 248, 252);

        foreach (var line in document.Lines)
        {
            var rowBg = altRow ? altBg : white;
            altRow = !altRow;

            PdfPCell MakeCell(string text, int align = Element.ALIGN_LEFT) =>
                new(new Phrase(text, cellFont)) { Padding = 5, HorizontalAlignment = align, VerticalAlignment = Element.ALIGN_MIDDLE, BackgroundColor = rowBg };

            table.AddCell(MakeCell(line.Product.Sku ?? string.Empty));

            var imgCell = new PdfPCell { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, BackgroundColor = rowBg, FixedHeight = 70 };
            var picture = (await _pictureService.GetPicturesByProductIdAsync(line.Product.Id, 1)).FirstOrDefault();
            if (picture != null)
            {
                var picBytes = await _pictureService.LoadPictureBinaryAsync(picture);
                if (picBytes?.Length > 0)
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

            var productCell = new PdfPCell { Padding = 5, HorizontalAlignment = Element.ALIGN_LEFT, VerticalAlignment = Element.ALIGN_MIDDLE, BackgroundColor = rowBg };
            productCell.AddElement(new Paragraph(new Anchor(line.Product.Name, linkFont) { Reference = await GetProductUrlAsync(line.Product, store.Url) }));
            if (!string.IsNullOrEmpty(line.AttributesFormatted))
                productCell.AddElement(new Paragraph(line.AttributesFormatted, attributeFont));
            table.AddCell(productCell);

            table.AddCell(MakeCell(line.UnitPriceFormatted, Element.ALIGN_RIGHT));
            table.AddCell(MakeCell(line.Item.Quantity.ToString(), Element.ALIGN_CENTER));
            table.AddCell(MakeCell(line.TotalFormatted, Element.ALIGN_RIGHT));
        }

        if (document.ShowPrices)
        {
            var footerBg = new BaseColor(230, 230, 230);
            table.AddCell(new PdfPCell(new Phrase("Total Amount :", metaLabelFont)) { Colspan = 5, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 7, BackgroundColor = footerBg });
            table.AddCell(new PdfPCell(new Phrase(document.SubTotalFormatted, metaLabelFont)) { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 7, BackgroundColor = footerBg });
        }

        doc.Add(table);

        if (!string.IsNullOrWhiteSpace(estimate.Note))
        {
            doc.Add(new Paragraph("Notes", metaLabelFont) { SpacingBefore = 16, SpacingAfter = 4 });
            doc.Add(new Paragraph(estimate.Note.Replace("\r\n", "\n"), metaValueFont) { Leading = 14 });
        }

        doc.Close();

        return stream.ToArray();
    }

    public string GetExportFileName(EstimateList estimate, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(estimate.Name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return $"{(string.IsNullOrEmpty(name) ? $"estimate-{estimate.Id}" : name)}.{extension}";
    }

    private async Task<IList<(string Label, string Value)>> GetMetadataAsync(EstimateList estimate)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var today = await _dateTimeHelper.ConvertToUserTimeAsync(DateTime.UtcNow, DateTimeKind.Utc);

        var metadata = new List<(string, string)>
        {
            ("Date :", today.ToString("MM/dd/yyyy")),
            ("Estimate Name :", estimate.Name),
            ("From :", store.Name)
        };

        if (estimate.CustomerId == 0 && !string.IsNullOrEmpty(estimate.ContactName))
            metadata.Add(("For :", estimate.ContactName
                + (string.IsNullOrEmpty(estimate.ContactEmail) ? string.Empty : $" — {estimate.ContactEmail}")));

        return metadata;
    }

    private async Task<string> GetProductUrlAsync(Product product, string storeUrl)
    {
        var relativeUrl = await _nopUrlHelper.RouteGenericUrlAsync<Product>(
            new { SeName = await _urlRecordService.GetSeNameAsync(product) });
        return storeUrl.TrimEnd('/') + relativeUrl;
    }
}
