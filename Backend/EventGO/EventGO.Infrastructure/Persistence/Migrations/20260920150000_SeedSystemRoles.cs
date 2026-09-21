using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EventGoDbContext))]
[Migration("20260920150000_SeedSystemRoles")]
public sealed class SeedSystemRoles : Migration
{
    private static readonly Guid CustomerRoleId =
        new("5efbc12c-03da-48d4-9df3-4fe496f0e72b");

    private static readonly Guid PlatformAdminRoleId =
        new("2ebce642-a7f7-4822-aa61-ace7bb12e7c9");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            IF NOT EXISTS (
                SELECT 1 FROM [AspNetRoles]
                WHERE [NormalizedName] = N'CUSTOMER')
            BEGIN
                INSERT INTO [AspNetRoles]
                    ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    ('{CustomerRoleId}', N'Customer', N'CUSTOMER',
                     N'09db20b5-8e10-4bd0-9136-87c94f9626aa');
            END;

            IF NOT EXISTS (
                SELECT 1 FROM [AspNetRoles]
                WHERE [NormalizedName] = N'PLATFORMADMIN')
            BEGIN
                INSERT INTO [AspNetRoles]
                    ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    ('{PlatformAdminRoleId}', N'PlatformAdmin', N'PLATFORMADMIN',
                     N'd15c9452-aaad-4210-93ba-e3aa28dd5e92');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            DELETE FROM [AspNetRoles]
            WHERE [Id] IN ('{CustomerRoleId}', '{PlatformAdminRoleId}')
              AND NOT EXISTS (
                  SELECT 1 FROM [AspNetUserRoles]
                  WHERE [RoleId] = [AspNetRoles].[Id]);
            """);
    }
}
