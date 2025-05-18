using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiryTime",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AudioData",
                table: "UserAnswers",
                type: "longblob",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationFeedback",
                table: "UserAnswers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EvaluationScore",
                table: "UserAnswers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscribedText",
                table: "UserAnswers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiryTime",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AudioData",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "EvaluationFeedback",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "EvaluationScore",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "TranscribedText",
                table: "UserAnswers");
        }
    }
}
