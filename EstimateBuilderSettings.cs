using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.EstimateBuilder;

public class EstimateBuilderSettings : ISettings
{
    /// <summary>
    /// Categories hidden from the estimate builder. Their subcategories are hidden too, and a product is
    /// excluded when every category it belongs to is excluded.
    /// </summary>
    public List<int> ExcludedCategoryIds { get; set; } = new();
}
