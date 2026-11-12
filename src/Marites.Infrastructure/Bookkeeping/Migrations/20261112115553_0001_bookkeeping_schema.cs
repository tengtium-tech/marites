using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marites.Infrastructure.Bookkeeping.Migrations
{
    /// <inheritdoc />
    public partial class _0001_bookkeeping_schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    entra_object_id = table.Column<string>(type: "TEXT", nullable: true),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    is_break_glass = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "broker_profile",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    provider_kind = table.Column<string>(type: "TEXT", nullable: false),
                    environment = table.Column<string>(type: "TEXT", nullable: false),
                    endpoint_enc = table.Column<byte[]>(type: "BLOB", nullable: false),
                    admin_uri = table.Column<string>(type: "TEXT", nullable: true),
                    credential_kind = table.Column<string>(type: "TEXT", nullable: false),
                    credentials_enc = table.Column<byte[]>(type: "BLOB", nullable: true),
                    credentials_updated_at = table.Column<string>(type: "TEXT", nullable: true),
                    namespace_or_vhost = table.Column<string>(type: "TEXT", nullable: true),
                    tls_opts = table.Column<string>(type: "TEXT", nullable: false),
                    capabilities_json = table.Column<string>(type: "TEXT", nullable: false),
                    is_default = table.Column<long>(type: "INTEGER", nullable: false),
                    preflight_state = table.Column<string>(type: "TEXT", nullable: false),
                    preflight_at = table.Column<string>(type: "TEXT", nullable: true),
                    revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "capture_rule",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    queue_ref_id = table.Column<string>(type: "TEXT", nullable: false),
                    bind_pattern = table.Column<string>(type: "TEXT", nullable: true),
                    enabled = table.Column<long>(type: "INTEGER", nullable: false),
                    capture_mode = table.Column<string>(type: "TEXT", nullable: false),
                    payload_policy = table.Column<string>(type: "TEXT", nullable: false),
                    sample_rate = table.Column<int>(type: "INTEGER", nullable: false),
                    row_retention_hours = table.Column<int>(type: "INTEGER", nullable: false),
                    max_rows = table.Column<long>(type: "INTEGER", nullable: true),
                    max_bytes = table.Column<long>(type: "INTEGER", nullable: true),
                    revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capture_rule", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "operation_intent",
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
                    bodies_json = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    expires_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_intent", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operation_payload",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    audit_id = table.Column<string>(type: "TEXT", nullable: false),
                    message_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_hash = table.Column<string>(type: "TEXT", nullable: true),
                    payload_before = table.Column<byte[]>(type: "BLOB", nullable: true),
                    payload_after = table.Column<byte[]>(type: "BLOB", nullable: true),
                    payload_size = table.Column<long>(type: "INTEGER", nullable: false),
                    retained_until = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_payload", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_ref",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    broker_profile_id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    source_kind = table.Column<string>(type: "TEXT", nullable: false),
                    vhost = table.Column<string>(type: "TEXT", nullable: true),
                    is_dlq = table.Column<long>(type: "INTEGER", nullable: false),
                    dead_letter_source = table.Column<string>(type: "TEXT", nullable: true),
                    bound_at = table.Column<string>(type: "TEXT", nullable: true),
                    capture_enabled = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_ref", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "replay_job",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    queue_ref_id = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    total = table.Column<int>(type: "INTEGER", nullable: false),
                    processed = table.Column<int>(type: "INTEGER", nullable: false),
                    failed = table.Column<int>(type: "INTEGER", nullable: false),
                    halt_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_by = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replay_job", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    slug = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_tenant_id",
                table: "app_user",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_broker_profile_tenant_id_name",
                table: "broker_profile",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_capture_rule_queue_ref_id",
                table: "capture_rule",
                column: "queue_ref_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_key_created_at",
                table: "idempotency_key",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_key_tenant_id_key_operation",
                table: "idempotency_key",
                columns: new[] { "tenant_id", "key", "operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operation_audit_tenant_id_created_at",
                table: "operation_audit",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_operation_intent_state",
                table: "operation_intent",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_operation_payload_retained_until",
                table: "operation_payload",
                column: "retained_until");

            migrationBuilder.CreateIndex(
                name: "IX_queue_ref_broker_profile_id_vhost_name",
                table: "queue_ref",
                columns: new[] { "broker_profile_id", "vhost", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_slug",
                table: "tenant",
                column: "slug",
                unique: true);

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
                name: "app_user");

            migrationBuilder.DropTable(
                name: "broker_profile");

            migrationBuilder.DropTable(
                name: "capture_rule");

            migrationBuilder.DropTable(
                name: "idempotency_key");

            migrationBuilder.DropTable(
                name: "operation_audit");

            migrationBuilder.DropTable(
                name: "operation_intent");

            migrationBuilder.DropTable(
                name: "operation_payload");

            migrationBuilder.DropTable(
                name: "queue_ref");

            migrationBuilder.DropTable(
                name: "replay_job");

            migrationBuilder.DropTable(
                name: "tenant");
        }
    }
}
