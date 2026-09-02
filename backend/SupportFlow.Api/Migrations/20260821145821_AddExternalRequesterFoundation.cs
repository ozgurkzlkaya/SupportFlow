using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRequesterFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Tickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ExternalRequesterId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SenderId",
                table: "TicketMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ExternalRequesterId",
                table: "TicketMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UploadedByUserId",
                table: "Attachments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "UploadedByExternalRequesterId",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalRequesters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRequesters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ExternalRequesterId",
                table: "Tickets",
                column: "ExternalRequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_ExternalRequesterId",
                table: "TicketMessages",
                column: "ExternalRequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedByExternalRequesterId",
                table: "Attachments",
                column: "UploadedByExternalRequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequesters_NormalizedEmail",
                table: "ExternalRequesters",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_ExternalRequesters_UploadedByExternalRequesterId",
                table: "Attachments",
                column: "UploadedByExternalRequesterId",
                principalTable: "ExternalRequesters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketMessages_ExternalRequesters_ExternalRequesterId",
                table: "TicketMessages",
                column: "ExternalRequesterId",
                principalTable: "ExternalRequesters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ExternalRequesters_ExternalRequesterId",
                table: "Tickets",
                column: "ExternalRequesterId",
                principalTable: "ExternalRequesters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_ExternalRequesters_UploadedByExternalRequesterId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketMessages_ExternalRequesters_ExternalRequesterId",
                table: "TicketMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ExternalRequesters_ExternalRequesterId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "ExternalRequesters");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ExternalRequesterId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_ExternalRequesterId",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_UploadedByExternalRequesterId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ExternalRequesterId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalRequesterId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "UploadedByExternalRequesterId",
                table: "Attachments");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SenderId",
                table: "TicketMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UploadedByUserId",
                table: "Attachments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
