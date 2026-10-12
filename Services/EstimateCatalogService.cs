using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.EstimateBuilder.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;

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
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductAttributeCombination> _combinationRepository;

    // Excluded categories plus all their descendants; computed once per request (the service is scoped)
    private HashSet<int>? _excludedCategoryIds;

    public EstimateCatalogService(
        ICategoryService categoryService,
        IProductService productService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        IPriceFormatter priceFormatter,
        IShoppingCartService shoppingCartService,
        ISettingService settingService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        IRepository<Product> productRepository,
        IRepository<ProductAttributeCombination> combinationRepository)
    {
        _categoryService = categoryService;
        _productService = productService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _priceFormatter = priceFormatter;
        _shoppingCartService = shoppingCartService;
        _settingService = settingService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _productRepository = productRepository;
        _combinationRepository = combinationRepository;
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

        var maxResults = hasKeywords ? 100 : 1000;

        // Keyword search reaches every product, including ones not assigned to any category
        IEnumerable<Product> products = await _productService.SearchProductsAsync(
            categoryIds: categoryIds,
            storeId: storeId,
            visibleIndividuallyOnly: true,
            keywords: hasKeywords ? keywords : null,
            searchSku: true,
            pageSize: maxResults,
            orderBy: ProductSortingEnum.NameAsc);

        // nopCommerce's standard search only matches a SKU when the whole SKU is typed, and a search provider
        // plugin (such as BetterSearch) matches whole words, fuzzily. Add literal partial name/SKU matches and
        // rank them first, so a provider's many loose matches for a short term can't push them past the cap.
        if (hasKeywords)
        {
            var literal = await SearchByPartialMatchAsync(keywords!, categoryIds, storeId, maxResults);
            var literalIds = literal.Select(p => p.Id).ToHashSet();
            var term = keywords!.ToLowerInvariant();

            var firstWord = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? term;

            // 0: name or SKU starts with the text, 1: a word in the name starts with it ("Duty Holster"),
            // 2: other literal matches ("Alcohol"), 3: provider-only (fuzzy) matches
            int Rank(Product p)
            {
                var name = (p.Name ?? string.Empty).ToLowerInvariant();
                if (name.StartsWith(term) || (p.Sku ?? string.Empty).ToLowerInvariant().StartsWith(term))
                    return 0;
                if (name.Contains(" " + firstWord))
                    return 1;
                return literalIds.Contains(p.Id) ? 2 : 3;
            }

            products = literal
                .Concat(products)
                .DistinctBy(p => p.Id)
                .OrderBy(Rank)
                .ThenBy(p => p.Name)
                .Take(maxResults);
        }

        var productList = products.ToList();
        var excludedProducts = await GetExcludedProductIdsAsync(productList.Select(p => p.Id).ToArray());

        return productList.Where(p => !excludedProducts.Contains(p.Id)).Select(p => (object)new
        {
            id = p.Id,
            name = string.IsNullOrEmpty(p.Sku) ? p.Name : $"{p.Name} ({p.Sku})"
        }).ToList();
    }

    /// <summary>
    /// Products whose name contains every typed word (in any order, as part of a word), or whose SKU,
    /// manufacturer part number or attribute-combination SKU contains the keywords. Filtered the way
    /// SearchProductsAsync filters: published and visible individually, plus ACL and store mapping on the
    /// storefront (storeId > 0), and restricted to <paramref name="categoryIds"/> when given.
    /// </summary>
    private async Task<IList<Product>> SearchByPartialMatchAsync(string keywords, IList<int>? categoryIds, int storeId, int maxResults)
    {
        var baseQuery = _productRepository.Table.Where(p => !p.Deleted && p.Published && p.VisibleIndividually);

        // Lower-case both sides: on case-sensitive databases (e.g. PostgreSQL) "hol" would otherwise miss "Holster"
        var term = keywords.ToLowerInvariant();
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

        var nameQuery = baseQuery;
        foreach (var word in words)
            nameQuery = nameQuery.Where(p => p.Name.ToLower().Contains(word));

        // Names starting with the first word, then names with a word starting with it, so they survive the cap
        var firstWord = words.FirstOrDefault() ?? term;
        var wordStart = " " + firstWord;
        var nameIds = await nameQuery
            .OrderBy(p => p.Name.ToLower().StartsWith(firstWord) ? 0 : p.Name.ToLower().Contains(wordStart) ? 1 : 2)
            .ThenBy(p => p.Name)
            .Select(p => p.Id)
            .Take(maxResults)
            .ToListAsync();

        var skuIds = await baseQuery
            .Where(p => (p.Sku != null && p.Sku.ToLower().Contains(term)) ||
                        (p.ManufacturerPartNumber != null && p.ManufacturerPartNumber.ToLower().Contains(term)))
            .Select(p => p.Id)
            .Take(maxResults)
            .ToListAsync();

        var productIds = nameIds.Concat(skuIds).ToList();

        var combinationProductIds = await _combinationRepository.Table
            .Where(c => c.Sku != null && c.Sku.ToLower().Contains(term))
            .Select(c => c.ProductId)
            .Distinct()
            .Take(maxResults)
            .ToListAsync();

        var ids = productIds.Concat(combinationProductIds).Distinct().ToArray();
        if (ids.Length == 0)
            return new List<Product>();

        var candidates = (await _productService.GetProductsByIdsAsync(ids))
            .Where(p => !p.Deleted && p.Published && p.VisibleIndividually)
            .ToList();

        if (categoryIds != null)
        {
            var allowed = categoryIds.ToHashSet();
            var productCategories = await _categoryService.GetProductCategoryIdsAsync(candidates.Select(p => p.Id).ToArray());
            candidates = candidates
                .Where(p => productCategories.TryGetValue(p.Id, out var cats) && cats.Any(allowed.Contains))
                .ToList();
        }

        if (storeId <= 0)
            return candidates;

        var visible = new List<Product>();
        foreach (var product in candidates)
            if (await _aclService.AuthorizeAsync(product) && await _storeMappingService.AuthorizeAsync(product, storeId))
                visible.Add(product);

        return visible;
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
