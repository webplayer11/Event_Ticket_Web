using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShrinkTicketCodeToVarchar20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddReservationInventoryFlow tạo cột varchar(50), còn model và
            // snapshot đã ghi varchar(20) mà không có migration nào thu cột,
            // nên `migrations add` không tự sinh được bước này.
            //
            // Vé phát hành trước đây mang mã 36 ký tự (TKT-{Guid:N}): cấp lại
            // mã đúng định dạng TKT-XXXX-XXXX-XXXX trước khi thu cột.
            migrationBuilder.Sql(
                """
                UPDATE [t]
                SET [TicketCode] = 'TKT-' + SUBSTRING([g].[Hex], 1, 4)
                    + '-' + SUBSTRING([g].[Hex], 5, 4)
                    + '-' + SUBSTRING([g].[Hex], 9, 4)
                FROM [Tickets] AS [t]
                CROSS APPLY (
                    SELECT REPLACE(CONVERT(char(36), NEWID()), '-', '') AS [Hex]
                ) AS [g]
                WHERE LEN([t].[TicketCode]) > 20;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TicketCode",
                table: "Tickets",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TicketCode",
                table: "Tickets",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20);
        }
    }
}
