using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateToCV : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Template",
                table: "CVs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Template",
                table: "CVs");
        }
    }
}
