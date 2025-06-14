using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class SetNullOnCvDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Interviews_CVs_CvId",
                table: "Interviews");

            migrationBuilder.AddForeignKey(
                name: "FK_Interviews_CVs_CvId",
                table: "Interviews",
                column: "CvId",
                principalTable: "CVs",
                principalColumn: "CvId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Interviews_CVs_CvId",
                table: "Interviews");

            migrationBuilder.AddForeignKey(
                name: "FK_Interviews_CVs_CvId",
                table: "Interviews",
                column: "CvId",
                principalTable: "CVs",
                principalColumn: "CvId");
        }
    }
}
