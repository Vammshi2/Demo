using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostelPro.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationRoomBedAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceAmount",
                table: "TenantApplications",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredBedId",
                table: "TenantApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredRoomId",
                table: "TenantApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomPrice",
                table: "TenantApplications",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_TenantApplications_PreferredBedId",
                table: "TenantApplications",
                column: "PreferredBedId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantApplications_PreferredRoomId",
                table: "TenantApplications",
                column: "PreferredRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantApplications_Beds_PreferredBedId",
                table: "TenantApplications",
                column: "PreferredBedId",
                principalTable: "Beds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantApplications_Rooms_PreferredRoomId",
                table: "TenantApplications",
                column: "PreferredRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantApplications_Beds_PreferredBedId",
                table: "TenantApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantApplications_Rooms_PreferredRoomId",
                table: "TenantApplications");

            migrationBuilder.DropIndex(
                name: "IX_TenantApplications_PreferredBedId",
                table: "TenantApplications");

            migrationBuilder.DropIndex(
                name: "IX_TenantApplications_PreferredRoomId",
                table: "TenantApplications");

            migrationBuilder.DropColumn(
                name: "AdvanceAmount",
                table: "TenantApplications");

            migrationBuilder.DropColumn(
                name: "PreferredBedId",
                table: "TenantApplications");

            migrationBuilder.DropColumn(
                name: "PreferredRoomId",
                table: "TenantApplications");

            migrationBuilder.DropColumn(
                name: "RoomPrice",
                table: "TenantApplications");
        }
    }
}
