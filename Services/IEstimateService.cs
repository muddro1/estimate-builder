using Nop.Plugin.Misc.EstimateBuilder.Domain;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public interface IEstimateService
{
    Task<IList<EstimateList>> GetEstimateListsByCustomerIdAsync(int customerId);
    Task<IList<EstimateList>> GetAllEstimateListsAsync();
    Task<EstimateList?> GetEstimateListByIdAsync(int id);
    Task InsertEstimateListAsync(EstimateList estimateList);
    Task UpdateEstimateListAsync(EstimateList estimateList);
    Task DeleteEstimateListAsync(EstimateList estimateList);
    Task CopyEstimateListAsync(int estimateListId, int customerId);

    Task<IList<EstimateListItem>> GetItemsByEstimateListIdAsync(int estimateListId);
    Task<IDictionary<int, int>> GetItemCountsAsync(IList<int> estimateListIds);

    /// <summary>
    /// Adds a product configuration to an estimate, or increases the quantity of the matching line
    /// (same product and same attributes). Quantities are capped at <see cref="EstimateBuilderDefaults.MaxQuantity"/>.
    /// </summary>
    Task AddOrIncrementItemAsync(int estimateListId, int productId, int quantity, string? attributesXml);

    Task UpdateEstimateListItemAsync(EstimateListItem item);
    Task DeleteEstimateListItemAsync(EstimateListItem item);
    Task DeleteEstimateListItemsByEstimateListIdAsync(int estimateListId);
}
