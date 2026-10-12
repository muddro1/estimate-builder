using Nop.Core;

namespace Nop.Plugin.Misc.EstimateBuilder.Domain;

public class EstimateList : BaseEntity
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Free text printed under the totals on the PDF and Excel exports</summary>
    public string? Note { get; set; }

    // Contact info for admin-created estimates not tied to a registered customer
    public string? ContactName { get; set; }
    public string? ContactCompany { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactAddress { get; set; }
    public string? ContactCity { get; set; }
    public string? ContactState { get; set; }
    public string? ContactZip { get; set; }
}
