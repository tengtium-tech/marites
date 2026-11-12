using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marites.Infrastructure.Bookkeeping.Migrations
{
    /// <inheritdoc />
    public partial class _0010_idempotency_key : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_key",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    operation = table.Column<string>(type: "TEXT", nullable: false),
                    request_hash = table.Column<string>(type: "TEXT", nullable: false),
                    response_status = table.Column<int>(type: "INTEGER", nullable: false),
                    response_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_key", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_key_created_at",
                table: "idempotency_key",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_key_tenant_id_key_operation",
                table: "idempotency_key",
                columns: new[] { "tenant_id", "key", "operation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_key");
        }
    }
}
