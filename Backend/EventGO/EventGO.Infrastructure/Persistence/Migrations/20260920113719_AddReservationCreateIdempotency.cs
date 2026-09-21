using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCreateIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Reservations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Reservations",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE dbo.Reservations
                SET
                    IdempotencyKey = CONCAT(
                        N'legacy-',
                        REPLACE(CONVERT(nvarchar(36), Id), N'-', N'')),
                    RequestHash = CONVERT(
                        varchar(64),
                        HASHBYTES('SHA2_256', CONVERT(varchar(36), Id)),
                        2)
                WHERE IdempotencyKey IS NULL OR RequestHash IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "Reservations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                table: "Reservations",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldUnicode: false,
                oldFixedLength: true,
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_UserId_IdempotencyKey",
                table: "Reservations",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Reservations_UserId_IdempotencyKey",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "Reservations");
        }
    }
}
