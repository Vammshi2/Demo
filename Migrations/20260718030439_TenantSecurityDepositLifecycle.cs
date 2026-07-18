using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostelPro.Migrations
{
    /// <inheritdoc />
    public partial class TenantSecurityDepositLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NoticeGivenDate",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoticePeriodDays",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedVacateDate",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositAmount",
                table: "Tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositDeductions",
                table: "Tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositPaidAmount",
                table: "Tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositRefundedAmount",
                table: "Tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SecurityDepositStatus",
                table: "Tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "VacateNotes",
                table: "Tenants",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "VacatedDate",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Tenants"
                SET "NoticePeriodDays" = 30,
                    "SecurityDepositStatus" = 'paid'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoticeGivenDate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NoticePeriodDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PlannedVacateDate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityDepositAmount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityDepositDeductions",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityDepositPaidAmount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityDepositRefundedAmount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityDepositStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "VacateNotes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "VacatedDate",
                table: "Tenants");
        }
    }
}
