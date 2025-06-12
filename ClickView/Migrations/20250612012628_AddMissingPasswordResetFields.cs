using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPasswordResetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenExpiry",
                table: "Users",
                newName: "PasswordResetCodeExpiry");

            migrationBuilder.RenameColumn(
                name: "PasswordResetToken",
                table: "Users",
                newName: "PasswordResetCode");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPasswordResetEmailSentAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPasswordResetEmailSentAt",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "PasswordResetCodeExpiry",
                table: "Users",
                newName: "PasswordResetTokenExpiry");

            migrationBuilder.RenameColumn(
                name: "PasswordResetCode",
                table: "Users",
                newName: "PasswordResetToken");
        }
    }
}
