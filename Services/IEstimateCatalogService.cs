using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.EstimateBuilder.Models;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

/// <summary>
/// Catalog lookups and attribute handling shared by the storefront and admin estimate pages.
/// </summary>
public interface IEstimateCatalogService
{
    Task<IList<CategorySelectItem>> GetCategoryOptionsAsync(int storeId);
    Task<IList<object>> GetProductOptionsAsync(int categoryId, int storeId);
    Task<object> GetAttributeOptionsAsync(Product product, bool includePriceAdjustments);

    /// <summary>
    /// Builds the attributes XML from posted <c>product_attribute_{mappingId}</c> fields (the same field names
    /// the product details page uses) and checks it against the product's attribute rules.
    /// Only mappings that belong to <paramref name="product"/> are read.
    /// </summary>
    /// <returns>The attributes XML and any problems that should block adding the item.</returns>
    Task<(string AttributesXml, IList<string> Errors)> ParseAndValidateAttributesAsync(
        Product product, IFormCollection form, Customer customer, int quantity);
}
