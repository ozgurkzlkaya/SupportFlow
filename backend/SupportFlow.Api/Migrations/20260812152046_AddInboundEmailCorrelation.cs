using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEmailCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketEmailThreads_TicketId",
                table: "TicketEmailThreads");

            migrationBuilder.AddColumn<string>(
                name: "InternetMessageId",
                table: "ProcessedEmailMessages",
                type: "nvarchar(998)",
                maxLength: 998,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketEmailThreads_TicketId",
                table: "TicketEmailThreads",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEmailMessages_InternetMessageId",
                table: "ProcessedEmailMessages",
                column: "InternetMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketEmailThreads_TicketId",
                table: "TicketEmailThreads");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedEmailMessages_InternetMessageId",
                table: "ProcessedEmailMessages");

            migrationBuilder.DropColumn(
                name: "InternetMessageId",
                table: "ProcessedEmailMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEmailThreads_TicketId",
                table: "TicketEmailThreads",
                column: "TicketId",
                unique: true);
        }
    }
}
