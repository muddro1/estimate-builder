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
    /// <summary>Excluded categories from the plugin settings, expanded with all their subcategories.</summary>
    Task<ISet<int>> GetExcludedCategoryIdsAsync();

    /// <summary>True when every category the product belongs to is excluded (uncategorized products never are).</summary>
    Task<bool> IsProductExcludedAsync(Product product);

    Task<IList<CategorySelectItem>> GetCategoryOptionsAsync(int storeId);

    /// <summary>
    /// Products for the "add product" picker: by category (including subcategories), by name/SKU keywords, or both.
    /// Returns nothing when neither is given.
    /// </summary>
    Task<IList<object>> GetProductOptionsAsync(int categoryId, string? keywords, int storeId);

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
