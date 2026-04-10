namespace Nop.Plugin.Misc.EstimateBuilder.Models;

public class EstimateItemModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceFormatted { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public string TotalFormatted { get; set; } = string.Empty;
    public bool Remove { get; set; }
    public string AttributesFormatted { get; set; } = string.Empty;
}

public class CategorySelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EstimateItemsModel
{
    public int EstimateListId { get; set; }
    public string EstimateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IList<EstimateItemModel> Items { get; set; } = new List<EstimateItemModel>();
    public string SubTotalFormatted { get; set; } = string.Empty;
    public IList<CategorySelectItem> Categories { get; set; } = new List<CategorySelectItem>();
}
