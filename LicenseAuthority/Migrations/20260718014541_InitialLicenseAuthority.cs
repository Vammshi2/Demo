using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostelPro.LicenseAuthority.Migrations
{
    /// <inheritdoc />
    public partial class InitialLicenseAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Licenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    KeyHash = table.Column<string>(type: "character(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaidThroughUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxInstallations = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                    table.CheckConstraint("CK_Licenses_MaxInstallations", "\"MaxInstallations\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "VendorAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorAdmins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HostName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ApplicationVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastValidatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseInstallations_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInstallations_LicenseId_InstallationId",
                table: "LicenseInstallations",
                columns: new[] { "LicenseId", "InstallationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInstallations_LicenseId_RevokedUtc",
                table: "LicenseInstallations",
                columns: new[] { "LicenseId", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_KeyHash",
                table: "Licenses",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_KeyPrefix",
                table: "Licenses",
                column: "KeyPrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_ProductCode_Status",
                table: "Licenses",
                columns: new[] { "ProductCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorAdmins_NormalizedEmail",
                table: "VendorAdmins",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseInstallations");

            migrationBuilder.DropTable(
                name: "VendorAdmins");

            migrationBuilder.DropTable(
                name: "Licenses");
        }
    }
}
