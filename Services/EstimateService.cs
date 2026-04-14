using Nop.Data;
using Nop.Plugin.Misc.EstimateBuilder.Domain;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public class EstimateService : IEstimateService
{
    private readonly IRepository<EstimateList> _estimateListRepository;
    private readonly IRepository<EstimateListItem> _estimateListItemRepository;

    public EstimateService(
        IRepository<EstimateList> estimateListRepository,
        IRepository<EstimateListItem> estimateListItemRepository)
    {
        _estimateListRepository = estimateListRepository;
        _estimateListItemRepository = estimateListItemRepository;
    }

    public async Task<IList<EstimateList>> GetEstimateListsByCustomerIdAsync(int customerId)
    {
        return await _estimateListRepository.GetAllAsync(q =>
            q.Where(e => e.CustomerId == customerId).OrderByDescending(e => e.CreatedOnUtc));
    }

    public async Task<IList<EstimateList>> GetAllEstimateListsAsync()
    {
        return await _estimateListRepository.GetAllAsync(q =>
            q.OrderByDescending(e => e.CreatedOnUtc));
    }

    public async Task<EstimateList?> GetEstimateListByIdAsync(int id)
    {
        return await _estimateListRepository.GetByIdAsync(id);
    }

    public async Task InsertEstimateListAsync(EstimateList estimateList)
    {
        await _estimateListRepository.InsertAsync(estimateList);
    }

    public async Task UpdateEstimateListAsync(EstimateList estimateList)
    {
        await _estimateListRepository.UpdateAsync(estimateList);
    }

    public async Task DeleteEstimateListAsync(EstimateList estimateList)
    {
        await DeleteEstimateListItemsByEstimateListIdAsync(estimateList.Id);
        await _estimateListRepository.DeleteAsync(estimateList);
    }

    public async Task CopyEstimateListAsync(int estimateListId, int customerId)
    {
        var original = await GetEstimateListByIdAsync(estimateListId);
        if (original == null)
            return;

        var copy = new EstimateList
        {
            CustomerId = customerId,
            Name = original.Name + " (Copy)",
            Description = original.Description,
            CreatedOnUtc = DateTime.UtcNow
        };
        await InsertEstimateListAsync(copy);

        var items = await GetItemsByEstimateListIdAsync(estimateListId);
        foreach (var item in items)
        {
            await InsertEstimateListItemAsync(new EstimateListItem
            {
                EstimateListId = copy.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity
            });
        }
    }

    public async Task<IList<EstimateListItem>> GetItemsByEstimateListIdAsync(int estimateListId)
    {
        return await _estimateListItemRepository.GetAllAsync(q =>
            q.Where(i => i.EstimateListId == estimateListId));
    }

    public async Task<EstimateListItem?> GetEstimateListItemAsync(int estimateListId, int productId)
    {
        return (await _estimateListItemRepository.GetAllAsync(q =>
            q.Where(i => i.EstimateListId == estimateListId && i.ProductId == productId))).FirstOrDefault();
    }

    public async Task InsertEstimateListItemAsync(EstimateListItem item)
    {
        await _estimateListItemRepository.InsertAsync(item);
    }

    public async Task UpdateEstimateListItemAsync(EstimateListItem item)
    {
        await _estimateListItemRepository.UpdateAsync(item);
    }

    public async Task DeleteEstimateListItemAsync(EstimateListItem item)
    {
        await _estimateListItemRepository.DeleteAsync(item);
    }

    public async Task DeleteEstimateListItemsByEstimateListIdAsync(int estimateListId)
    {
        var items = await GetItemsByEstimateListIdAsync(estimateListId);
        await _estimateListItemRepository.DeleteAsync(items.ToList());
    }
}
