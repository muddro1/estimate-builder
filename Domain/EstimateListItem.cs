using Nop.Core;

namespace Nop.Plugin.Misc.EstimateBuilder.Domain;

public class EstimateListItem : BaseEntity
{
    public int EstimateListId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
