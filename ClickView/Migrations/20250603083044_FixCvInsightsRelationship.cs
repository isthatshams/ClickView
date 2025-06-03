using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class FixCvInsightsRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnhancedCvText",
                table: "CvEnhancements");

            migrationBuilder.DropColumn(
                name: "Suggestions",
                table: "CvEnhancements");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "CVs",
                newName: "UploadDate");

            migrationBuilder.UpdateData(
                table: "CVs",
                keyColumn: "ExtractedText",
                keyValue: null,
                column: "ExtractedText",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedText",
                table: "CVs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "CVs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "CVs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "CVs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "CVs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedDate",
                table: "CVs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CVs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnhancedCvJson",
                table: "CvEnhancements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SuggestionsJson",
                table: "CvEnhancements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "ProcessedDate",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "EnhancedCvJson",
                table: "CvEnhancements");

            migrationBuilder.DropColumn(
                name: "SuggestionsJson",
                table: "CvEnhancements");

            migrationBuilder.RenameColumn(
                name: "UploadDate",
                table: "CVs",
                newName: "UploadedAt");

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedText",
                table: "CVs",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnhancedCvText",
                table: "CvEnhancements",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Suggestions",
                table: "CvEnhancements",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
