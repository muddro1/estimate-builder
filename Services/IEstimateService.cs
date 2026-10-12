using Nop.Core;
using Nop.Plugin.Misc.EstimateBuilder.Domain;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public interface IEstimateService
{
    Task<IList<EstimateList>> GetEstimateListsByCustomerIdAsync(int customerId);

    /// <summary>
    /// Newest first, filtered by estimate name, contact name/email or owner name/email.
    /// </summary>
    Task<IPagedList<EstimateList>> SearchEstimateListsAsync(string? searchTerm, int pageIndex, int pageSize);
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
