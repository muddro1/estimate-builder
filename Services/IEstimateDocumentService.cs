using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.EstimateBuilder.Domain;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

/// <summary>
/// One priced line of an estimate. Prices are already tax-adjusted and converted to the working currency.
/// </summary>
public record EstimateLine(
    EstimateListItem Item,
    Product Product,
    decimal UnitPrice,
    decimal Total,
    string UnitPriceFormatted,
    string TotalFormatted,
    string AttributesFormatted,
    bool PriceAvailable);

public record EstimateDocument(
    EstimateList Estimate,
    IList<EstimateLine> Lines,
    decimal SubTotal,
    string SubTotalFormatted,
    string CurrencyCode,
    bool ShowPrices);

public interface IEstimateDocumentService
{
    /// <summary>
    /// Prices every line the way the store would for <paramref name="pricingCustomer"/>
    /// (tier/role/special prices, discounts, attribute adjustments, tax display and working currency).
    /// </summary>
    Task<EstimateDocument> PrepareAsync(EstimateList estimate, Customer pricingCustomer, bool showPrices);

    /// <summary>
    /// The customer whose prices an estimate should show: its owner, or a guest for contact-only estimates.
    /// </summary>
    Task<Customer> GetPricingCustomerAsync(EstimateList estimate);

    Task<byte[]> ExportToExcelAsync(EstimateDocument document);
    Task<byte[]> ExportToPdfAsync(EstimateDocument document);
    string GetExportFileName(EstimateList estimate, string extension);
}
