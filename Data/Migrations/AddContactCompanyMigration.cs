using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

[NopMigration("2026/10/12 13:00:00", "Misc.EstimateBuilder add ContactCompany", MigrationProcessType.Update)]
public class AddContactCompanyMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table(EstimateBuilderDefaults.EstimateListTable).Column("ContactCompany").Exists())
            Alter.Table(EstimateBuilderDefaults.EstimateListTable).AddColumn("ContactCompany").AsString(500).Nullable();
    }

    public override void Down()
    {
        if (Schema.Table(EstimateBuilderDefaults.EstimateListTable).Column("ContactCompany").Exists())
            Delete.Column("ContactCompany").FromTable(EstimateBuilderDefaults.EstimateListTable);
    }
}
