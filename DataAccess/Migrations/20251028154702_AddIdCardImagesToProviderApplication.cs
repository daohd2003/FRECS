using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIdCardImagesToProviderApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdCardBackImageUrl",
                table: "ProviderApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardFrontImageUrl",
                table: "ProviderApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdCardBackImageUrl",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "IdCardFrontImageUrl",
                table: "ProviderApplications");
        }
    }
}
