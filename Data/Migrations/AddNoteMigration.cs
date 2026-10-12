using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

[NopMigration("2026/10/12 12:00:00", "Misc.EstimateBuilder add Note", MigrationProcessType.Update)]
public class AddNoteMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table(EstimateBuilderDefaults.EstimateListTable).Column("Note").Exists())
            Alter.Table(EstimateBuilderDefaults.EstimateListTable).AddColumn("Note").AsString(4000).Nullable();
    }

    public override void Down()
    {
        if (Schema.Table(EstimateBuilderDefaults.EstimateListTable).Column("Note").Exists())
            Delete.Column("Note").FromTable(EstimateBuilderDefaults.EstimateListTable);
    }
}
