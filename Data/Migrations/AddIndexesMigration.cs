using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

/// <summary>
/// Adds lookup indexes to installs created before they were part of <see cref="SchemaMigration"/>.
/// </summary>
[NopMigration("2026/10/11 12:00:00", "Misc.EstimateBuilder add indexes", MigrationProcessType.Update)]
public class AddIndexesMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table(EstimateBuilderDefaults.EstimateListTable).Index(EstimateBuilderDefaults.EstimateListCustomerIndex).Exists())
            Create.Index(EstimateBuilderDefaults.EstimateListCustomerIndex)
                .OnTable(EstimateBuilderDefaults.EstimateListTable).OnColumn("CustomerId");

        if (!Schema.Table(EstimateBuilderDefaults.EstimateListItemTable).Index(EstimateBuilderDefaults.EstimateListItemListIndex).Exists())
            Create.Index(EstimateBuilderDefaults.EstimateListItemListIndex)
                .OnTable(EstimateBuilderDefaults.EstimateListItemTable).OnColumn("EstimateListId");
    }

    public override void Down()
    {
        if (Schema.Table(EstimateBuilderDefaults.EstimateListTable).Index(EstimateBuilderDefaults.EstimateListCustomerIndex).Exists())
            Delete.Index(EstimateBuilderDefaults.EstimateListCustomerIndex).OnTable(EstimateBuilderDefaults.EstimateListTable);

        if (Schema.Table(EstimateBuilderDefaults.EstimateListItemTable).Index(EstimateBuilderDefaults.EstimateListItemListIndex).Exists())
            Delete.Index(EstimateBuilderDefaults.EstimateListItemListIndex).OnTable(EstimateBuilderDefaults.EstimateListItemTable);
    }
}
