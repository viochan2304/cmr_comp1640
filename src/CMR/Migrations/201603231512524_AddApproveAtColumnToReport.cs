namespace CMR.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddApproveAtColumnToReport : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Reports", "ApproveAt", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Reports", "ApproveAt");
        }
    }
}
