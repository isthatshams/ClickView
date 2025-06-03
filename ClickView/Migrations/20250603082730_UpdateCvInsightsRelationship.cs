using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCvInsightsRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CvInsights_CvId",
                table: "CvInsights");

            migrationBuilder.DropColumn(
                name: "InsightsJson",
                table: "CVs");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "CVs");

            migrationBuilder.AlterColumn<string>(
                name: "ToolsAndTechnologies",
                table: "CvInsights",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalSkills",
                table: "CvInsights",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SoftSkills",
                table: "CvInsights",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ExperienceSummary",
                table: "CvInsights",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Certifications",
                table: "CvInsights",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CvInsights_CvId",
                table: "CvInsights",
                column: "CvId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CvInsights_CvId",
                table: "CvInsights");

            migrationBuilder.AddColumn<string>(
                name: "InsightsJson",
                table: "CVs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "CVs",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "CvInsights",
                keyColumn: "ToolsAndTechnologies",
                keyValue: null,
                column: "ToolsAndTechnologies",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ToolsAndTechnologies",
                table: "CvInsights",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "CvInsights",
                keyColumn: "TechnicalSkills",
                keyValue: null,
                column: "TechnicalSkills",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalSkills",
                table: "CvInsights",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "CvInsights",
                keyColumn: "SoftSkills",
                keyValue: null,
                column: "SoftSkills",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "SoftSkills",
                table: "CvInsights",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "CvInsights",
                keyColumn: "ExperienceSummary",
                keyValue: null,
                column: "ExperienceSummary",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ExperienceSummary",
                table: "CvInsights",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "CvInsights",
                keyColumn: "Certifications",
                keyValue: null,
                column: "Certifications",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Certifications",
                table: "CvInsights",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CvInsights_CvId",
                table: "CvInsights",
                column: "CvId");
        }
    }
}
