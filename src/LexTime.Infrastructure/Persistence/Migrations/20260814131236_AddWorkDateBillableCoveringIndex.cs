using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkDateBillableCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_WorkDate_Billable",
                schema: "dbo",
                table: "TimeEntries",
                columns: new[] { "WorkDate", "IsBillable" })
                .Annotation("SqlServer:Include", new[] { "MatterId", "DurationMinutes", "HourlyRateSnapshot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_WorkDate_Billable",
                schema: "dbo",
                table: "TimeEntries");
        }
    }
}
