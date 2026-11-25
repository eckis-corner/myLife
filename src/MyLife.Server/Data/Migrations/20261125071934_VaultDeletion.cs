using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VaultDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_scheduled_at",
                table: "emergency_switches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "rekey_areas",
                table: "emergency_switches",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "rekey_letters",
                table: "emergency_switches",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "vault_deletion_votes",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trustee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agreed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_deletion_votes", x => new { x.owner_id, x.trustee_id });
                    table.ForeignKey(
                        name: "FK_vault_deletion_votes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vault_deletion_votes_users_trustee_id",
                        column: x => x.trustee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vault_deletion_votes_trustee_id",
                table: "vault_deletion_votes",
                column: "trustee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vault_deletion_votes");

            migrationBuilder.DropColumn(
                name: "deletion_scheduled_at",
                table: "emergency_switches");

            migrationBuilder.DropColumn(
                name: "rekey_areas",
                table: "emergency_switches");

            migrationBuilder.DropColumn(
                name: "rekey_letters",
                table: "emergency_switches");
        }
    }
}
