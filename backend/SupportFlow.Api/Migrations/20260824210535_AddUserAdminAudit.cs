using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAdminAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    TargetDisplayName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    TargetEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TargetRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OldIsActive = table.Column<bool>(type: "bit", nullable: false),
                    NewIsActive = table.Column<bool>(type: "bit", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAdminAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAdminAudits_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserAdminAudits_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAdminAudits_ActorUserId",
                table: "UserAdminAudits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAdminAudits_TargetUserId_OccurredAt",
                table: "UserAdminAudits",
                columns: new[] { "TargetUserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAdminAudits");
        }
    }
}
