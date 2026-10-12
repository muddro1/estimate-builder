using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Misc.EstimateBuilder.Models;

public class EstimateListModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(EstimateBuilderDefaults.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(EstimateBuilderDefaults.DescriptionMaxLength)]
    public string? Description { get; set; }

    public string CreatedOn { get; set; } = string.Empty;
}

public class EstimateListsModel
{
    public IList<EstimateListModel> Estimates { get; set; } = new List<EstimateListModel>();
}
