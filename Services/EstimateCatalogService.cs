using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Services.Catalog;
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

    public EstimateCatalogService(
        ICategoryService categoryService,
        IProductService productService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        IPriceFormatter priceFormatter,
        IShoppingCartService shoppingCartService)
    {
        _categoryService = categoryService;
        _productService = productService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _priceFormatter = priceFormatter;
        _shoppingCartService = shoppingCartService;
    }

    public async Task<IList<CategorySelectItem>> GetCategoryOptionsAsync(int storeId)
    {
        var allCategories = await _categoryService.GetAllCategoriesAsync(storeId);
        var items = new List<CategorySelectItem>();
        foreach (var category in allCategories)
            items.Add(new CategorySelectItem
            {
                Id = category.Id,
                Name = await _categoryService.GetFormattedBreadCrumbAsync(category, allCategories)
            });

        return items.OrderBy(c => c.Name).ToList();
    }

    public async Task<IList<object>> GetProductOptionsAsync(int categoryId, int storeId)
    {
        // A parent category lists the products of all its subcategories too (at any depth)
        var categoryIds = new List<int> { categoryId };
        categoryIds.AddRange(await _categoryService.GetChildCategoryIdsAsync(categoryId, storeId));

        var products = await _productService.SearchProductsAsync(
            categoryIds: categoryIds.Distinct().ToList(),
            storeId: storeId,
            visibleIndividuallyOnly: true,
            pageSize: 1000,
            orderBy: ProductSortingEnum.NameAsc);

        return products.Select(p => (object)new { id = p.Id, name = p.Name }).ToList();
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
