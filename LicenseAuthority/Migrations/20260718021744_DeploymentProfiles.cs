using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostelPro.LicenseAuthority.Migrations
{
    /// <inheritdoc />
    public partial class DeploymentProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationUrl",
                table: "Licenses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeploymentRegion",
                table: "Licenses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeploymentStatus",
                table: "Licenses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "not_configured");

            migrationBuilder.AddColumn<string>(
                name: "HostingProvider",
                table: "Licenses",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDeployedUtc",
                table: "Licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecretReference",
                table: "Licenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationUrl",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "DeploymentRegion",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "DeploymentStatus",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "HostingProvider",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "LastDeployedUtc",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "SecretReference",
                table: "Licenses");
        }
    }
}
