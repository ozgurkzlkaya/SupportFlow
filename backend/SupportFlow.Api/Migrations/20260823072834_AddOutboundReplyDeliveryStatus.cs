using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundReplyDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "TicketMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryAttemptCount",
                table: "TicketMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeliveryAttemptAt",
                table: "TicketMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundDeliveryError",
                table: "TicketMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundDeliveryStatus",
                table: "TicketMessages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.Sql("""
                UPDATE message
                SET [OutboundDeliveryStatus] = N'Sent'
                FROM [TicketMessages] AS message
                INNER JOIN [Users] AS sender ON sender.[Id] = message.[SenderId]
                WHERE message.[IsInternal] = CAST(0 AS bit)
                  AND sender.[Role] IN (N'Admin', N'TechnicalStaff');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "DeliveryAttemptCount",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "LastDeliveryAttemptAt",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "OutboundDeliveryError",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "OutboundDeliveryStatus",
                table: "TicketMessages");
        }
    }
}
