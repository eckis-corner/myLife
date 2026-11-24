using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyLife.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Users : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    auth_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kdf_salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    kdf_memory_kib = table.Column<int>(type: "integer", nullable: false),
                    kdf_iterations = table.Column<int>(type: "integer", nullable: false),
                    kdf_parallelism = table.Column<int>(type: "integer", nullable: false),
                    wrapped_vmk = table.Column<byte[]>(type: "bytea", nullable: false),
                    wrapped_vmk_by_recovery = table.Column<byte[]>(type: "bytea", nullable: false),
                    public_encryption_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    wrapped_encryption_private_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    public_signing_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    wrapped_signing_private_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    totp_secret = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
