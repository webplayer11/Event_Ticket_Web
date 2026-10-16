using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EventGoDbContext))]
[Migration("20261016160000_SeedOrganizerRole")]
public sealed class SeedOrganizerRole : Migration
{
    private static readonly Guid OrganizerRoleId =
        new("7c1f3d2a-5b8e-4c6d-9a0f-3e2b1d4c5a6f");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            IF NOT EXISTS (
                SELECT 1 FROM [AspNetRoles]
                WHERE [NormalizedName] = N'ORGANIZER')
            BEGIN
                INSERT INTO [AspNetRoles]
                    ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    ('{OrganizerRoleId}', N'Organizer', N'ORGANIZER',
                     N'4a9e6c1b-2d3f-4e5a-8b7c-0d1e2f3a4b5c');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            DELETE FROM [AspNetRoles]
            WHERE [Id] = '{OrganizerRoleId}'
              AND NOT EXISTS (
                  SELECT 1 FROM [AspNetUserRoles]
                  WHERE [RoleId] = [AspNetRoles].[Id]);
            """);
    }
}
