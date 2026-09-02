using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailInboundTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketEmailThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    GmailThreadId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEmailThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEmailThreads_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcessedEmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketEmailThreadId = table.Column<int>(type: "int", nullable: false),
                    GmailMessageId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TicketMessageId = table.Column<int>(type: "int", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedEmailMessages_TicketEmailThreads_TicketEmailThreadId",
                        column: x => x.TicketEmailThreadId,
                        principalTable: "TicketEmailThreads",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcessedEmailMessages_TicketMessages_TicketMessageId",
                        column: x => x.TicketMessageId,
                        principalTable: "TicketMessages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEmailMessages_GmailMessageId",
                table: "ProcessedEmailMessages",
                column: "GmailMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEmailMessages_TicketEmailThreadId",
                table: "ProcessedEmailMessages",
                column: "TicketEmailThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEmailMessages_TicketMessageId",
                table: "ProcessedEmailMessages",
                column: "TicketMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEmailThreads_GmailThreadId",
                table: "TicketEmailThreads",
                column: "GmailThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketEmailThreads_TicketId",
                table: "TicketEmailThreads",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedEmailMessages");

            migrationBuilder.DropTable(
                name: "TicketEmailThreads");
        }
    }
}
