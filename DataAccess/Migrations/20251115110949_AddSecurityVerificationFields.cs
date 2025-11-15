using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessLicenseImageUrl",
                table: "ProviderApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdAddress",
                table: "ProviderApplications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CccdConfidenceScore",
                table: "ProviderApplications",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdDateOfBirth",
                table: "ProviderApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdFullName",
                table: "ProviderApplications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdIdNumber",
                table: "ProviderApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdSex",
                table: "ProviderApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdVerificationError",
                table: "ProviderApplications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CccdVerified",
                table: "ProviderApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CccdVerifiedAt",
                table: "ProviderApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FaceMatchScore",
                table: "ProviderApplications",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FaceMatched",
                table: "ProviderApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrivacyPolicyAgreed",
                table: "ProviderApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyPolicyAgreedAt",
                table: "ProviderApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderType",
                table: "ProviderApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SelfieImageUrl",
                table: "ProviderApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessLicenseImageUrl",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdAddress",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdConfidenceScore",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdDateOfBirth",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdFullName",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdIdNumber",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdSex",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdVerificationError",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdVerified",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "CccdVerifiedAt",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "FaceMatchScore",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "FaceMatched",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyAgreed",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyAgreedAt",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "ProviderApplications");

            migrationBuilder.DropColumn(
                name: "SelfieImageUrl",
                table: "ProviderApplications");
        }
    }
}
