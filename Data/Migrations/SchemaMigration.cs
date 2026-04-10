using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

[NopMigration("2024/01/01 12:00:00", "Misc.EstimateBuilder schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        Create.Table("EstimateList")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CustomerId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(500).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("CreatedOnUtc").AsDateTime2().NotNullable();

        Create.Table("EstimateListItem")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("EstimateListId").AsInt32().NotNullable()
            .WithColumn("ProductId").AsInt32().NotNullable()
            .WithColumn("Quantity").AsInt32().NotNullable().WithDefaultValue(1);
    }
}
