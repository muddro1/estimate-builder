using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public class EstimateCatalogService : IEstimateCatalogService
{
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductAttributeParser _productAttributeParser;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly ISettingService _settingService;

    // Excluded categories plus all their descendants; computed once per request (the service is scoped)
    private HashSet<int>? _excludedCategoryIds;

    public EstimateCatalogService(
        ICategoryService categoryService,
        IProductService productService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        IPriceFormatter priceFormatter,
        IShoppingCartService shoppingCartService,
        ISettingService settingService)
    {
        _categoryService = categoryService;
        _productService = productService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _priceFormatter = priceFormatter;
        _shoppingCartService = shoppingCartService;
        _settingService = settingService;
    }

    public async Task<ISet<int>> GetExcludedCategoryIdsAsync()
    {
        if (_excludedCategoryIds != null)
            return _excludedCategoryIds;

        var settings = await _settingService.LoadSettingAsync<EstimateBuilderSettings>();
        var excluded = new HashSet<int>();
        foreach (var categoryId in settings.ExcludedCategoryIds.Where(id => id > 0).Distinct())
        {
            excluded.Add(categoryId);
            excluded.UnionWith(await _categoryService.GetChildCategoryIdsAsync(categoryId, showHidden: true));
        }

        return _excludedCategoryIds = excluded;
    }

    public async Task<bool> IsProductExcludedAsync(Product product)
    {
        return (await GetExcludedProductIdsAsync(new[] { product.Id })).Contains(product.Id);
    }

    /// <summary>
    /// A product is excluded when it has categories and every one of them is excluded;
    /// uncategorized products and products also listed in an allowed category stay available.
    /// </summary>
    private async Task<ISet<int>> GetExcludedProductIdsAsync(int[] productIds)
    {
        var excludedCategories = await GetExcludedCategoryIdsAsync();
        if (!excludedCategories.Any() || productIds.Length == 0)
            return new HashSet<int>();

        var productCategories = await _categoryService.GetProductCategoryIdsAsync(productIds);
        return productCategories
            .Where(pc => pc.Value.Length > 0 && pc.Value.All(excludedCategories.Contains))
            .Select(pc => pc.Key)
            .ToHashSet();
    }

    public async Task<IList<CategorySelectItem>> GetCategoryOptionsAsync(int storeId)
    {
        var allCategories = await _categoryService.GetAllCategoriesAsync(storeId);
        var excluded = await GetExcludedCategoryIdsAsync();
        var items = new List<CategorySelectItem>();
        foreach (var category in allCategories.Where(c => !excluded.Contains(c.Id)))
            items.Add(new CategorySelectItem
            {
                Id = category.Id,
                Name = await _categoryService.GetFormattedBreadCrumbAsync(category, allCategories)
            });

        return items.OrderBy(c => c.Name).ToList();
    }

    public async Task<IList<object>> GetProductOptionsAsync(int categoryId, string? keywords, int storeId)
    {
        keywords = keywords?.Trim();
        var hasKeywords = !string.IsNullOrEmpty(keywords) && keywords.Length >= 2;
        if (categoryId <= 0 && !hasKeywords)
            return new List<object>();

        var excludedCategories = await GetExcludedCategoryIdsAsync();
        if (excludedCategories.Contains(categoryId))
            return new List<object>();

        // A parent category lists the products of all its (non-excluded) subcategories too, at any depth
        List<int>? categoryIds = null;
        if (categoryId > 0)
        {
            categoryIds = new List<int> { categoryId };
            categoryIds.AddRange(await _categoryService.GetChildCategoryIdsAsync(categoryId, storeId));
            categoryIds = categoryIds.Distinct().Where(id => !excludedCategories.Contains(id)).ToList();
        }

        // Keyword search reaches every product, including ones not assigned to any category
        var products = await _productService.SearchProductsAsync(
            categoryIds: categoryIds,
            storeId: storeId,
            visibleIndividuallyOnly: true,
            keywords: hasKeywords ? keywords : null,
            searchSku: true,
            pageSize: hasKeywords ? 100 : 1000,
            orderBy: ProductSortingEnum.NameAsc);

        var excludedProducts = await GetExcludedProductIdsAsync(products.Select(p => p.Id).ToArray());

        return products.Where(p => !excludedProducts.Contains(p.Id)).Select(p => (object)new
        {
            id = p.Id,
            name = string.IsNullOrEmpty(p.Sku) ? p.Name : $"{p.Name} ({p.Sku})"
        }).ToList();
    }

    public async Task<object> GetAttributeOptionsAsync(Product product, bool includePriceAdjustments)
    {
        var mappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id);
        if (!mappings.Any())
            return new { hasAttributes = false };

        var result = new List<object>();
        foreach (var mapping in mappings)
        {
            var attribute = await _productAttributeService.GetProductAttributeByIdAsync(mapping.ProductAttributeId);
            var values = await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id);

            var valueModels = new List<object>();
            foreach (var value in values)
            {
                var adjustment = string.Empty;
                if (includePriceAdjustments && value.PriceAdjustment != decimal.Zero)
                    adjustment = value.PriceAdjustmentUsePercentage
                        ? $"{(value.PriceAdjustment > 0 ? "+" : "")}{value.PriceAdjustment:0.##}%"
                        : (value.PriceAdjustment > 0 ? "+" : "-") + await _priceFormatter.FormatPriceAsync(Math.Abs(value.PriceAdjustment));

                valueModels.Add(new
                {
                    id = value.Id,
                    name = value.Name,
                    priceAdjustmentStr = adjustment,
                    isPreSelected = value.IsPreSelected,
                    colorSquaresRgb = value.ColorSquaresRgb ?? string.Empty
                });
            }

            result.Add(new
            {
                mappingId = mapping.Id,
                name = attribute?.Name ?? string.Empty,
                isRequired = mapping.IsRequired,
                controlType = (int)mapping.AttributeControlType,
                defaultValue = mapping.DefaultValue ?? string.Empty,
                values = valueModels
            });
        }

        return new { hasAttributes = true, attributes = result };
    }

    public async Task<(string AttributesXml, IList<string> Errors)> ParseAndValidateAttributesAsync(
        Product product, IFormCollection form, Customer customer, int quantity)
    {
        var errors = new List<string>();
        string attributesXml;
        try
        {
            attributesXml = await _productAttributeParser.ParseProductAttributesAsync(product, form, errors);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // The parser int.Parse()s posted value ids
            return (string.Empty, new List<string> { "Invalid attribute selection." });
        }

        // Required attributes, value ownership, conditional attributes, text length limits, etc.
        errors.AddRange(await _shoppingCartService.GetShoppingCartItemAttributeWarningsAsync(
            customer, ShoppingCartType.ShoppingCart, product, quantity, attributesXml));

        return (attributesXml, errors.Distinct().ToList());
    }
}
