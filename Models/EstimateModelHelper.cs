using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.EstimateBuilder.Services;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Models;

/// <summary>
/// Model plumbing shared by the storefront and admin estimate controllers.
/// </summary>
public static class EstimateModelHelper
{
    /// <returns>An error message, or null when both values are acceptable.</returns>
    public static string? ValidateNameAndDescription(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Estimate name is required.";
        if (name.Trim().Length > EstimateBuilderDefaults.NameMaxLength)
            return $"Estimate name can't be longer than {EstimateBuilderDefaults.NameMaxLength} characters.";
        if (description?.Trim().Length > EstimateBuilderDefaults.DescriptionMaxLength)
            return $"Description can't be longer than {EstimateBuilderDefaults.DescriptionMaxLength} characters.";

        return null;
    }

    /// <summary>
    /// Applies posted quantity changes and removals to the lines of one estimate.
    /// Posted ids that don't belong to the estimate are ignored.
    /// </summary>
    /// <param name="removeItemId">Line removed by its row's delete button, or 0</param>
    public static async Task ApplyItemChangesAsync(IEstimateService estimateService, int estimateListId,
        IList<EstimateItemModel> postedItems, int removeItemId = 0)
    {
        var items = (await estimateService.GetItemsByEstimateListIdAsync(estimateListId)).ToDictionary(i => i.Id);

        foreach (var posted in postedItems)
        {
            if (!items.TryGetValue(posted.Id, out var item))
                continue;

            if (posted.Remove || posted.Id == removeItemId)
            {
                await estimateService.DeleteEstimateListItemAsync(item);
                continue;
            }

            var quantity = Math.Clamp(posted.Quantity, 1, EstimateBuilderDefaults.MaxQuantity);
            if (item.Quantity == quantity)
                continue;

            item.Quantity = quantity;
            await estimateService.UpdateEstimateListItemAsync(item);
        }
    }

    public static async Task<EstimateItemModel> ToItemModelAsync(EstimateLine line,
        IPictureService pictureService, IUrlRecordService urlRecordService, INopUrlHelper nopUrlHelper)
    {
        var imageUrl = string.Empty;
        var picture = (await pictureService.GetPicturesByProductIdAsync(line.Product.Id, 1)).FirstOrDefault();
        if (picture != null)
            imageUrl = await pictureService.GetPictureUrlAsync(picture.Id, 75);

        return new EstimateItemModel
        {
            Id = line.Item.Id,
            ProductId = line.Product.Id,
            Sku = line.Product.Sku ?? string.Empty,
            ProductName = line.Product.Name,
            ProductUrl = await nopUrlHelper.RouteGenericUrlAsync<Product>(
                new { SeName = await urlRecordService.GetSeNameAsync(line.Product) }),
            ImageUrl = imageUrl,
            Price = line.UnitPrice,
            PriceFormatted = line.UnitPriceFormatted,
            Quantity = line.Item.Quantity,
            Total = line.Total,
            TotalFormatted = line.TotalFormatted,
            AttributesFormatted = line.AttributesFormatted
        };
    }
}
