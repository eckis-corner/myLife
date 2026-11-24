using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reference_id",
                table: "one_time_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trustee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    owner_mail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_requests_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_requests_users_trustee_id",
                        column: x => x.trustee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "emergency_switches",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    waiting_days = table.Column<int>(type: "integer", nullable: false),
                    request_waiting_days = table.Column<int>(type: "integer", nullable: false),
                    second_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ntfy_topic = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_lifesign_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    overdue_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    stage_mail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stage_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_modes = table.Column<int>(type: "integer", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_switches", x => x.owner_id);
                    table.ForeignKey(
                        name: "FK_emergency_switches_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_owner_id_state",
                table: "access_requests",
                columns: new[] { "owner_id", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_trustee_id",
                table: "access_requests",
                column: "trustee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_requests");

            migrationBuilder.DropTable(
                name: "emergency_switches");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "one_time_tokens");
        }
    }
}
