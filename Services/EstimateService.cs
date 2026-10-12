using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.Misc.EstimateBuilder.Domain;

namespace Nop.Plugin.Misc.EstimateBuilder.Services;

public class EstimateService : IEstimateService
{
    private readonly IRepository<EstimateList> _estimateListRepository;
    private readonly IRepository<EstimateListItem> _estimateListItemRepository;
    private readonly IRepository<Customer> _customerRepository;

    public EstimateService(
        IRepository<EstimateList> estimateListRepository,
        IRepository<EstimateListItem> estimateListItemRepository,
        IRepository<Customer> customerRepository)
    {
        _estimateListRepository = estimateListRepository;
        _estimateListItemRepository = estimateListItemRepository;
        _customerRepository = customerRepository;
    }

    public async Task<IList<EstimateList>> GetEstimateListsByCustomerIdAsync(int customerId)
    {
        return await _estimateListRepository.GetAllAsync(q =>
            q.Where(e => e.CustomerId == customerId).OrderByDescending(e => e.CreatedOnUtc));
    }

    public async Task<IPagedList<EstimateList>> SearchEstimateListsAsync(string? searchTerm, int pageIndex, int pageSize)
    {
        var term = searchTerm?.Trim();

        // Search and paging run in the database; the owner's name/email come from a left join to Customer
        var query =
            from e in _estimateListRepository.Table
            join c in _customerRepository.Table on e.CustomerId equals c.Id into owners
            from c in owners.DefaultIfEmpty()
            where string.IsNullOrEmpty(term)
                  || e.Name.Contains(term)
                  || (e.ContactName != null && e.ContactName.Contains(term))
                  || (e.ContactEmail != null && e.ContactEmail.Contains(term))
                  || (c != null && (
                      (c.Email != null && c.Email.Contains(term))
                      || (c.FirstName != null && c.FirstName.Contains(term))
                      || (c.LastName != null && c.LastName.Contains(term))
                      || ((c.FirstName ?? "") + " " + (c.LastName ?? "")).Contains(term)))
            orderby e.CreatedOnUtc descending, e.Id descending
            select e;

        return await query.ToPagedListAsync(pageIndex, pageSize);
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
            Name = TrimToLength(original.Name + " (Copy)", EstimateBuilderDefaults.NameMaxLength),
            Description = original.Description,
            CreatedOnUtc = DateTime.UtcNow,
            ContactName = original.ContactName,
            ContactEmail = original.ContactEmail,
            ContactPhone = original.ContactPhone,
            ContactAddress = original.ContactAddress,
            ContactCity = original.ContactCity,
            ContactState = original.ContactState,
            ContactZip = original.ContactZip
        };
        await InsertEstimateListAsync(copy);

        var items = await GetItemsByEstimateListIdAsync(estimateListId);
        await _estimateListItemRepository.InsertAsync(items.Select(item => new EstimateListItem
        {
            EstimateListId = copy.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            AttributesXml = item.AttributesXml
        }).ToList());
    }

    public async Task<IList<EstimateListItem>> GetItemsByEstimateListIdAsync(int estimateListId)
    {
        return await _estimateListItemRepository.GetAllAsync(q =>
            q.Where(i => i.EstimateListId == estimateListId).OrderBy(i => i.Id));
    }

    public async Task<IDictionary<int, int>> GetItemCountsAsync(IList<int> estimateListIds)
    {
        if (!estimateListIds.Any())
            return new Dictionary<int, int>();

        var counts = await _estimateListItemRepository.Table
            .Where(i => estimateListIds.Contains(i.EstimateListId))
            .GroupBy(i => i.EstimateListId)
            .Select(g => new { EstimateListId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(c => c.EstimateListId, c => c.Count);
    }

    public async Task AddOrIncrementItemAsync(int estimateListId, int productId, int quantity, string? attributesXml)
    {
        attributesXml = string.IsNullOrEmpty(attributesXml) ? null : attributesXml;
        quantity = Math.Clamp(quantity, 1, EstimateBuilderDefaults.MaxQuantity);

        var existing = (await GetItemsByEstimateListIdAsync(estimateListId)).FirstOrDefault(i =>
            i.ProductId == productId &&
            string.Equals(i.AttributesXml ?? string.Empty, attributesXml ?? string.Empty, StringComparison.Ordinal));

        if (existing != null)
        {
            existing.Quantity = (int)Math.Min((long)existing.Quantity + quantity, EstimateBuilderDefaults.MaxQuantity);
            await _estimateListItemRepository.UpdateAsync(existing);
            return;
        }

        await _estimateListItemRepository.InsertAsync(new EstimateListItem
        {
            EstimateListId = estimateListId,
            ProductId = productId,
            Quantity = quantity,
            AttributesXml = attributesXml
        });

        // Two requests racing (e.g. a double-click) can both insert; fold any duplicates into the oldest line.
        // AttributesXml is too long for a unique index, so this is enforced here instead of in the schema.
        var duplicates = (await GetItemsByEstimateListIdAsync(estimateListId))
            .Where(i => i.ProductId == productId &&
                        string.Equals(i.AttributesXml ?? string.Empty, attributesXml ?? string.Empty, StringComparison.Ordinal))
            .OrderBy(i => i.Id)
            .ToList();
        if (duplicates.Count > 1)
        {
            var keep = duplicates[0];
            keep.Quantity = (int)Math.Min(duplicates.Sum(i => (long)i.Quantity), EstimateBuilderDefaults.MaxQuantity);
            await _estimateListItemRepository.UpdateAsync(keep);
            await _estimateListItemRepository.DeleteAsync(duplicates.Skip(1).ToList());
        }
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

    private static string TrimToLength(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
