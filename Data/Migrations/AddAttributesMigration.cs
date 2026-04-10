using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

[NopMigration("2024/01/15 12:00:00", "Misc.EstimateBuilder add AttributesXml", MigrationProcessType.Update)]
public class AddAttributesMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table("EstimateListItem").Column("AttributesXml").Exists())
            Alter.Table("EstimateListItem").AddColumn("AttributesXml").AsString(4000).Nullable();
    }

    public override void Down()
    {
        if (Schema.Table("EstimateListItem").Column("AttributesXml").Exists())
            Delete.Column("AttributesXml").FromTable("EstimateListItem");
    }
}
