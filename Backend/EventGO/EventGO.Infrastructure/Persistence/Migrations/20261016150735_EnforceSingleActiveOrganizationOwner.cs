using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveOrganizationOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_SingleActiveOwner",
                table: "OrganizationMembers",
                column: "OrganizationId",
                unique: true,
                filter: "[Role] = 1 AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationMembers_SingleActiveOwner",
                table: "OrganizationMembers");
        }
    }
}
