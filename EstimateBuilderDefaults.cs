namespace Nop.Plugin.Misc.EstimateBuilder;

public static class EstimateBuilderDefaults
{
    public const string SystemName = "Misc.EstimateBuilder";

    public const string EstimateListTable = "EstimateList";
    public const string EstimateListItemTable = "EstimateListItem";
    public const string EstimateListCustomerIndex = "IX_EstimateList_CustomerId";
    public const string EstimateListItemListIndex = "IX_EstimateListItem_EstimateListId";

    public const int NameMaxLength = 500;
    public const int DescriptionMaxLength = 4000;

    /// <summary>Upper bound for a single line's quantity, so repeated adds can't overflow.</summary>
    public const int MaxQuantity = 100_000;

    public const string ViewsPath = "~/Plugins/Misc.EstimateBuilder/Views";
    public const string ScriptPath = "~/Plugins/Misc.EstimateBuilder/Content/estimate-builder.js";
}
