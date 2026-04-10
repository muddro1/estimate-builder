using Nop.Core;

namespace Nop.Plugin.Misc.EstimateBuilder.Domain;

public class EstimateList : BaseEntity
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
