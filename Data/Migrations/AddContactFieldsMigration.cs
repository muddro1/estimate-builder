using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Misc.EstimateBuilder.Data.Migrations;

[NopMigration("2024/02/01 12:00:00", "Misc.EstimateBuilder add contact fields", MigrationProcessType.Update)]
public class AddContactFieldsMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table("EstimateList").Column("ContactName").Exists())
            Alter.Table("EstimateList").AddColumn("ContactName").AsString(500).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactEmail").Exists())
            Alter.Table("EstimateList").AddColumn("ContactEmail").AsString(500).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactPhone").Exists())
            Alter.Table("EstimateList").AddColumn("ContactPhone").AsString(100).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactAddress").Exists())
            Alter.Table("EstimateList").AddColumn("ContactAddress").AsString(500).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactCity").Exists())
            Alter.Table("EstimateList").AddColumn("ContactCity").AsString(200).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactState").Exists())
            Alter.Table("EstimateList").AddColumn("ContactState").AsString(200).Nullable();
        if (!Schema.Table("EstimateList").Column("ContactZip").Exists())
            Alter.Table("EstimateList").AddColumn("ContactZip").AsString(50).Nullable();
    }

    public override void Down()
    {
        foreach (var col in new[] { "ContactName", "ContactEmail", "ContactPhone", "ContactAddress", "ContactCity", "ContactState", "ContactZip" })
            if (Schema.Table("EstimateList").Column(col).Exists())
                Delete.Column(col).FromTable("EstimateList");
    }
}
