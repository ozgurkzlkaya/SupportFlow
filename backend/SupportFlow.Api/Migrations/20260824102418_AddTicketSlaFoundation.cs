using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionCycleStartedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FirstResponseDueAt",
                table: "Tickets",
                column: "FirstResponseDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResolutionDueAt",
                table: "Tickets",
                column: "ResolutionDueAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_FirstResponseDueAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResolutionDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstResponseDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionCycleStartedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Tickets");
        }
    }
}
