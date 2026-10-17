using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EventGO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalOrganizationsAndEventCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonalOwnerUserId",
                table: "Organizations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.InsertData(
                table: "EventCategories",
                columns: new[] { "Id", "Description", "IsActive", "Name", "Slug" },
                values: new object[,]
                {
                    { new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01"), "", true, "Âm nhạc", "am-nhac" },
                    { new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b02"), "", true, "Thể thao", "the-thao" },
                    { new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b03"), "", true, "Hội thảo/Workshop", "hoi-thao-workshop" },
                    { new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b04"), "", true, "Sân khấu & Nghệ thuật", "san-khau-nghe-thuat" },
                    { new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b05"), "", true, "Khác", "khac" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PersonalOwnerUserId",
                table: "Organizations",
                column: "PersonalOwnerUserId",
                unique: true,
                filter: "[PersonalOwnerUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_AspNetUsers_PersonalOwnerUserId",
                table: "Organizations",
                column: "PersonalOwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_AspNetUsers_PersonalOwnerUserId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_PersonalOwnerUserId",
                table: "Organizations");

            migrationBuilder.DeleteData(
                table: "EventCategories",
                keyColumn: "Id",
                keyValue: new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01"));

            migrationBuilder.DeleteData(
                table: "EventCategories",
                keyColumn: "Id",
                keyValue: new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b02"));

            migrationBuilder.DeleteData(
                table: "EventCategories",
                keyColumn: "Id",
                keyValue: new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b03"));

            migrationBuilder.DeleteData(
                table: "EventCategories",
                keyColumn: "Id",
                keyValue: new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b04"));

            migrationBuilder.DeleteData(
                table: "EventCategories",
                keyColumn: "Id",
                keyValue: new Guid("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b05"));

            migrationBuilder.DropColumn(
                name: "PersonalOwnerUserId",
                table: "Organizations");
        }
    }
}
