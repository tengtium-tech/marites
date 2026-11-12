using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marites.Infrastructure.Bookkeeping.Migrations
{
    /// <inheritdoc />
    public partial class _0003_broker_profile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_broker_profile_tenant_id_name",
                table: "broker_profile",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broker_profile");
        }
    }
}
