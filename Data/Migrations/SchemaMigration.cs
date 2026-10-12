using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

/// <summary>
/// Creates the full current schema on a fresh install.
/// nopCommerce only marks Update migrations as applied when a plugin is installed (it does not run them),
/// so every column and index added by a later Update migration must also be created here.
/// </summary>
[NopMigration("2024/01/01 12:00:00", "Misc.EstimateBuilder schema", MigrationProcessType.Installation)]
public class SchemaMigration : Migration
{
    public override void Up()
    {
        Create.Table(EstimateBuilderDefaults.EstimateListTable)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CustomerId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(500).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("CreatedOnUtc").AsDateTime2().NotNullable()
            .WithColumn("ContactName").AsString(500).Nullable()
            .WithColumn("ContactEmail").AsString(500).Nullable()
            .WithColumn("ContactPhone").AsString(100).Nullable()
            .WithColumn("ContactAddress").AsString(500).Nullable()
            .WithColumn("ContactCity").AsString(200).Nullable()
            .WithColumn("ContactState").AsString(200).Nullable()
            .WithColumn("ContactZip").AsString(50).Nullable();

        Create.Table(EstimateBuilderDefaults.EstimateListItemTable)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("EstimateListId").AsInt32().NotNullable()
            .WithColumn("ProductId").AsInt32().NotNullable()
            .WithColumn("Quantity").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("AttributesXml").AsString(4000).Nullable();

        Create.Index(EstimateBuilderDefaults.EstimateListCustomerIndex)
            .OnTable(EstimateBuilderDefaults.EstimateListTable).OnColumn("CustomerId");

        Create.Index(EstimateBuilderDefaults.EstimateListItemListIndex)
            .OnTable(EstimateBuilderDefaults.EstimateListItemTable).OnColumn("EstimateListId");
    }

    public override void Down()
    {
        // Indexes go away with their tables
        if (Schema.Table(EstimateBuilderDefaults.EstimateListItemTable).Exists())
            Delete.Table(EstimateBuilderDefaults.EstimateListItemTable);
        if (Schema.Table(EstimateBuilderDefaults.EstimateListTable).Exists())
            Delete.Table(EstimateBuilderDefaults.EstimateListTable);
    }
}
