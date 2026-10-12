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
        // Search and paging run in the database; the owner's details come from a left join to Customer
        var query =
            from e in _estimateListRepository.Table
            join c in _customerRepository.Table on e.CustomerId equals c.Id into owners
            from c in owners.DefaultIfEmpty()
            select new { e, c };

        // Every word must match at least one field, in any order. Both sides are lower-cased so the search
        // is case-insensitive on every database, including case-sensitive ones such as PostgreSQL.
        var words = (searchTerm ?? string.Empty).ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct();

        foreach (var word in words)
        {
            // "#1042" means estimate number 1042 only; a bare number also matches text (a ZIP, a phone...)
            if (word.StartsWith('#'))
            {
                var exactId = int.TryParse(word[1..], out var parsedId) ? parsedId : -1;
                query = query.Where(x => x.e.Id == exactId);
                continue;
            }

            var w = word;
            var id = int.TryParse(w, out var numericId) ? numericId : -1;
            // Phone numbers match on digits alone, so "5551234" finds "(555) 123-4567"; words with fewer than
            // 3 digits skip the phone check (the placeholder below never occurs in a phone number)
            var digits = new string(w.Where(char.IsDigit).ToArray());
            var phoneDigits = digits.Length >= 3 ? digits : "\u0001";

            query = query.Where(x =>
                x.e.Id == id
                || x.e.Name.ToLower().Contains(w)
                || (x.e.ContactName != null && x.e.ContactName.ToLower().Contains(w))
                || (x.e.ContactCompany != null && x.e.ContactCompany.ToLower().Contains(w))
                || (x.e.ContactEmail != null && x.e.ContactEmail.ToLower().Contains(w))
                || (x.e.ContactCity != null && x.e.ContactCity.ToLower().Contains(w))
                || (x.e.ContactPhone != null && x.e.ContactPhone
                    .Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace(".", "").Replace("+", "")
                    .Contains(phoneDigits))
                || (x.c != null && (
                    (x.c.Email != null && x.c.Email.ToLower().Contains(w))
                    || (x.c.FirstName != null && x.c.FirstName.ToLower().Contains(w))
                    || (x.c.LastName != null && x.c.LastName.ToLower().Contains(w))
                    || (x.c.Company != null && x.c.Company.ToLower().Contains(w))
                    || (x.c.City != null && x.c.City.ToLower().Contains(w))
                    || (x.c.Phone != null && x.c.Phone
                        .Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace(".", "").Replace("+", "")
                        .Contains(phoneDigits)))));
        }

        return await query
            .OrderByDescending(x => x.e.CreatedOnUtc)
            .ThenByDescending(x => x.e.Id)
            .Select(x => x.e)
            .ToPagedListAsync(pageIndex, pageSize);
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
            Note = original.Note,
            CreatedOnUtc = DateTime.UtcNow,
            ContactName = original.ContactName,
            ContactCompany = original.ContactCompany,
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
