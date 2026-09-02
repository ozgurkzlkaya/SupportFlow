using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketActivityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ExternalRequesterId = table.Column<int>(type: "int", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelatedStatusHistoryId = table.Column<int>(type: "int", nullable: true),
                    RelatedMessageId = table.Column<int>(type: "int", nullable: true),
                    RelatedAttachmentId = table.Column<int>(type: "int", nullable: true),
                    MetadataVersion = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketActivities", x => x.Id);
                    table.CheckConstraint("CK_TicketActivities_ActorKind", "([ActorKind] = 'User' AND [ActorUserId] IS NOT NULL AND [ExternalRequesterId] IS NULL) OR ([ActorKind] = 'ExternalRequester' AND [ActorUserId] IS NULL AND [ExternalRequesterId] IS NOT NULL) OR ([ActorKind] = 'System' AND [ActorUserId] IS NULL AND [ExternalRequesterId] IS NULL)");
                    table.CheckConstraint("CK_TicketActivities_MetadataVersion", "[MetadataVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TicketActivities_Attachments_RelatedAttachmentId",
                        column: x => x.RelatedAttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketActivities_ExternalRequesters_ExternalRequesterId",
                        column: x => x.ExternalRequesterId,
                        principalTable: "ExternalRequesters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketActivities_TicketMessages_RelatedMessageId",
                        column: x => x.RelatedMessageId,
                        principalTable: "TicketMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketActivities_TicketStatusHistories_RelatedStatusHistoryId",
                        column: x => x.RelatedStatusHistoryId,
                        principalTable: "TicketStatusHistories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketActivities_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ActivityType_OccurredAt",
                table: "TicketActivities",
                columns: new[] { "ActivityType", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ActorUserId_OccurredAt",
                table: "TicketActivities",
                columns: new[] { "ActorUserId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ExternalRequesterId_OccurredAt",
                table: "TicketActivities",
                columns: new[] { "ExternalRequesterId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_RelatedAttachmentId",
                table: "TicketActivities",
                column: "RelatedAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_RelatedMessageId",
                table: "TicketActivities",
                column: "RelatedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_RelatedStatusHistoryId",
                table: "TicketActivities",
                column: "RelatedStatusHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId_OccurredAt_Id",
                table: "TicketActivities",
                columns: new[] { "TicketId", "OccurredAt", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketActivities");
        }
    }
}
