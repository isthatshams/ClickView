using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFinishedAndFinishedAtToInterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPasswordResetEmailSentAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeExpiry",
                table: "Users");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Interviews",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "Interviews",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "Interviews");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPasswordResetEmailSentAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetCode",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetCodeExpiry",
                table: "Users",
                type: "datetime(6)",
                nullable: true);
        }
    }
}
