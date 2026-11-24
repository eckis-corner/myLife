using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Trust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "wrapped_key",
                table: "objects",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trustee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    area_key = table.Column<int>(type: "integer", nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    envelope = table.Column<byte[]>(type: "bytea", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: false),
                    fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grants", x => x.id);
                    table.ForeignKey(
                        name: "FK_grants_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grants_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grants_users_trustee_id",
                        column: x => x.trustee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trust_relations",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trustee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_profile = table.Column<byte[]>(type: "bytea", nullable: false),
                    can_request_access = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_relations", x => new { x.owner_id, x.trustee_id });
                    table.ForeignKey(
                        name: "FK_trust_relations_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trust_relations_users_trustee_id",
                        column: x => x.trustee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grants_object_id",
                table: "grants",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "IX_grants_owner_id_trustee_id",
                table: "grants",
                columns: new[] { "owner_id", "trustee_id" });

            migrationBuilder.CreateIndex(
                name: "IX_grants_owner_id_trustee_id_kind_area_key_object_id_mode",
                table: "grants",
                columns: new[] { "owner_id", "trustee_id", "kind", "area_key", "object_id", "mode" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_grants_trustee_id_mode",
                table: "grants",
                columns: new[] { "trustee_id", "mode" });

            migrationBuilder.CreateIndex(
                name: "IX_trust_relations_trustee_id",
                table: "trust_relations",
                column: "trustee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grants");

            migrationBuilder.DropTable(
                name: "trust_relations");

            migrationBuilder.DropColumn(
                name: "wrapped_key",
                table: "objects");
        }
    }
}
