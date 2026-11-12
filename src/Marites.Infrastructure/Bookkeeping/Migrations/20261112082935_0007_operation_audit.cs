using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marites.Infrastructure.Bookkeeping.Migrations
{
    /// <inheritdoc />
    public partial class _0007_operation_audit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operation_audit",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    actor_id = table.Column<string>(type: "TEXT", nullable: true),
                    actor_kind = table.Column<string>(type: "TEXT", nullable: false),
                    on_behalf_of = table.Column<string>(type: "TEXT", nullable: true),
                    proposal_id = table.Column<string>(type: "TEXT", nullable: true),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    target = table.Column<string>(type: "TEXT", nullable: false),
                    params_json = table.Column<string>(type: "TEXT", nullable: false),
                    result = table.Column<string>(type: "TEXT", nullable: false),
                    message_count = table.Column<int>(type: "INTEGER", nullable: true),
                    payload_hash = table.Column<string>(type: "TEXT", nullable: true),
                    payload_edited = table.Column<long>(type: "INTEGER", nullable: false),
                    via = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    ip = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_audit", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operation_audit_tenant_id_created_at",
                table: "operation_audit",
                columns: new[] { "tenant_id", "created_at" });

            // Append-only by construction, not by convention (FR-016). A repository that forgets
            // cannot update or delete a row, and neither can anyone with a SQLite browser.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER operation_audit_no_update
                BEFORE UPDATE ON operation_audit
                BEGIN
                    SELECT RAISE(ABORT, 'operation_audit is append-only: updates are refused (FR-016)');
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER operation_audit_no_delete
                BEFORE DELETE ON operation_audit
                BEGIN
                    SELECT RAISE(ABORT, 'operation_audit is append-only: deletes are refused (FR-016)');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operation_audit");
        }
    }
}
