using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Reminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminder_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_months = table.Column<int>(type: "integer", nullable: false),
                    lead_days = table.Column<int>(type: "integer", nullable: false),
                    mail_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    notified_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    last_review_mail_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_reminder_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminder_settings");
        }
    }
}
