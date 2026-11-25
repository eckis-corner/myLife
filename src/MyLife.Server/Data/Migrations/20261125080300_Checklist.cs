using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Checklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checklist_progress",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    done_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist_progress", x => x.task_id);
                    table.ForeignKey(
                        name: "FK_checklist_progress_objects_task_id",
                        column: x => x.task_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checklist_progress_users_done_by_id",
                        column: x => x.done_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checklist_progress_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checklist_progress_done_by_id",
                table: "checklist_progress",
                column: "done_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_progress_owner_id",
                table: "checklist_progress",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist_progress");
        }
    }
}
