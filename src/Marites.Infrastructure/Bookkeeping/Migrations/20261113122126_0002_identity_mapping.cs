using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marites.Infrastructure.Bookkeeping.Migrations
{
    /// <inheritdoc />
    public partial class _0002_identity_mapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "entra_object_id",
                table: "app_user",
                newName: "subject");

            migrationBuilder.AddColumn<string>(
                name: "issuer",
                table: "app_user",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sessions_revoked_at",
                table: "app_user",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_tenant_id_issuer_subject",
                table: "app_user",
                columns: new[] { "tenant_id", "issuer", "subject" },
                unique: true,
                filter: "issuer IS NOT NULL AND subject IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_app_user_tenant_id_issuer_subject",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "issuer",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "sessions_revoked_at",
                table: "app_user");

            migrationBuilder.RenameColumn(
                name: "subject",
                table: "app_user",
                newName: "entra_object_id");
        }
    }
}
