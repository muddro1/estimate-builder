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
    Task<EstimateListItem?> GetEstimateListItemAsync(int estimateListId, int productId);
    Task InsertEstimateListItemAsync(EstimateListItem item);
    Task UpdateEstimateListItemAsync(EstimateListItem item);
    Task DeleteEstimateListItemAsync(EstimateListItem item);
    Task DeleteEstimateListItemsByEstimateListIdAsync(int estimateListId);
}
