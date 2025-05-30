using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickView.Migrations
{
    /// <inheritdoc />
    public partial class AddNewAttributesToYourEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnswerAnalysisId",
                table: "UserAnswers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentQuestionId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "CVs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AnswerAnalyses",
                columns: table => new
                {
                    AnswerAnalysisId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tone = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PersonalityTraits = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SoftSkills = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAnswerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerAnalyses", x => x.AnswerAnalysisId);
                    table.ForeignKey(
                        name: "FK_AnswerAnalyses_UserAnswers_UserAnswerId",
                        column: x => x.UserAnswerId,
                        principalTable: "UserAnswers",
                        principalColumn: "UserAnswerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CareerReports",
                columns: table => new
                {
                    CareerReportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    Strengths = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weaknesses = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SkillsJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobSuggestionsJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerReports", x => x.CareerReportId);
                    table.ForeignKey(
                        name: "FK_CareerReports_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "InterviewId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CvEnhancements",
                columns: table => new
                {
                    CvEnhancementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CvId = table.Column<int>(type: "int", nullable: false),
                    JobTitle = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Suggestions = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnhancedCvText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvEnhancements", x => x.CvEnhancementId);
                    table.ForeignKey(
                        name: "FK_CvEnhancements_CVs_CvId",
                        column: x => x.CvId,
                        principalTable: "CVs",
                        principalColumn: "CvId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FeedbackReports",
                columns: table => new
                {
                    FeedbackReportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Strengths = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weaknesses = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PersonalitySummary = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Recommendation = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InterviewId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackReports", x => x.FeedbackReportId);
                    table.ForeignKey(
                        name: "FK_FeedbackReports_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "InterviewId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecommendedCourses",
                columns: table => new
                {
                    RecommendedCourseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Url = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CareerReportId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendedCourses", x => x.RecommendedCourseId);
                    table.ForeignKey(
                        name: "FK_RecommendedCourses_CareerReports_CareerReportId",
                        column: x => x.CareerReportId,
                        principalTable: "CareerReports",
                        principalColumn: "CareerReportId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAnalyses_UserAnswerId",
                table: "AnswerAnalyses",
                column: "UserAnswerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerReports_InterviewId",
                table: "CareerReports",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CvEnhancements_CvId",
                table: "CvEnhancements",
                column: "CvId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReports_InterviewId",
                table: "FeedbackReports",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedCourses_CareerReportId",
                table: "RecommendedCourses",
                column: "CareerReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerAnalyses");

            migrationBuilder.DropTable(
                name: "CvEnhancements");

            migrationBuilder.DropTable(
                name: "FeedbackReports");

            migrationBuilder.DropTable(
                name: "RecommendedCourses");

            migrationBuilder.DropTable(
                name: "CareerReports");

            migrationBuilder.DropColumn(
                name: "AnswerAnalysisId",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "ParentQuestionId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "CVs");
        }
    }
}
