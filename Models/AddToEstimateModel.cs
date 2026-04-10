namespace Nop.Plugin.Misc.EstimateBuilder.Models;

public class AddToEstimateModel
{
    public int ProductId { get; set; }
    public int EstimateListId { get; set; }
    public int Quantity { get; set; } = 1;
    public IList<EstimateListSelectItem> Estimates { get; set; } = new List<EstimateListSelectItem>();
}

public class EstimateListSelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
