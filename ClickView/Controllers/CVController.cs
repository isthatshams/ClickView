using Microsoft.AspNetCore.Mvc;
using ClickView.Data;
using ClickView.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using System.Text;
using ClickView.Services;
using ClickView.DTOs;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CVController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly CvEnhancerService _cvEnhancerService;

        public CVController(ApplicationDbContext db, CvEnhancerService cvEnhancerService)
        {
            _db = db;
            _cvEnhancerService = cvEnhancerService;
        }

        [HttpPost("{userId}/upload")]
        [HttpPost("{userId}/upload-cv")]
        public async Task<IActionResult> UploadCv(int userId, IFormFile file)
        {
            if (file == null || file.Length == 0 || Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest("Please upload a valid PDF.");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var content = ms.ToArray();

            var extractedText = ExtractTextFromPdf(content);

            var cv = new CV
            {
                UserId = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = content,
                ExtractedText = extractedText
            };

            _db.CVs.Add(cv);
            await _db.SaveChangesAsync(); // Save to get CvId

            // 🔍 Extract insights via Flask
            try
            {
                var insights = await ExtractInsightsFromFlask(extractedText);
                if (insights != null)
                {
                    insights.CvId = cv.CvId;
                    _db.CvInsights.Add(insights);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to extract insights: " + ex.Message);
            }

            return Ok(new { message = "CV uploaded and text extracted.", cvId = cv.CvId });
        }
        private async Task<CvInsights?> ExtractInsightsFromFlask(string cvText)
        {
            using var httpClient = new HttpClient();
            var payload = new { cv_text = cvText };
            var flaskUrl = "http://my-ngrok-url.ngrok-free.app/extract-insights"; //replace with Flask endpoint

            var response = await httpClient.PostAsJsonAsync(flaskUrl, payload);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();

            return new CvInsights
            {
                TechnicalSkills = string.Join(", ", json.GetValueOrDefault("technicalSkills", new())),
                SoftSkills = string.Join(", ", json.GetValueOrDefault("softSkills", new())),
                ToolsAndTechnologies = string.Join(", ", json.GetValueOrDefault("tools", new())),
                Certifications = string.Join(", ", json.GetValueOrDefault("certifications", new())),
                ExperienceSummary = string.Join(", ", json.GetValueOrDefault("experience", new()))
            };
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserCvs(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            var cvs = await _db.CVs
                .Where(c => c.UserId == userId)
                .Select(c => new
                {
                    c.CvId,
                    c.FileName,
                    c.ContentType,
                    c.UploadedAt,
                    HasExtractedText = !string.IsNullOrWhiteSpace(c.ExtractedText)
                })
                .ToListAsync();

            return Ok(cvs);
        }

        private string ExtractTextFromPdf(byte[] pdfData)
        {
            using var ms = new MemoryStream(pdfData);
            using var doc = PdfDocument.Open(ms);
            var text = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                text.AppendLine(page.Text);
            }

            return text.ToString();
        }

        [HttpPost("{id}/enhance")]
        public async Task<IActionResult> EnhanceCv(int id, [FromBody] EnhanceCvRequestDto dto)
        {
            var cv = _db.CVs.Include(c => c.Insights).FirstOrDefault(c => c.CvId == id);
            if (cv == null) return NotFound("CV not found.");
            var cvText = !string.IsNullOrWhiteSpace(cv.ExtractedText)
                ? cv.ExtractedText
                : cv.Insights != null
                    ? $"{cv.Insights.TechnicalSkills}\n{cv.Insights.ToolsAndTechnologies}\n{cv.Insights.SoftSkills}\n{cv.Insights.Certifications}\n{cv.Insights.ExperienceSummary}"
                    : null;
            if (string.IsNullOrWhiteSpace(cvText))
                return BadRequest("No CV text or insights available.");
            var result = await _cvEnhancerService.EnhanceCvAsync(cvText, dto.JobTitle);
            var enhancement = new CvEnhancement
            {
                CvId = id,
                JobTitle = dto.JobTitle,
                Suggestions = result.Suggestions,
                EnhancedCvText = result.EnhancedCv,
                CreatedAt = DateTime.UtcNow
            };
            _db.CvEnhancements.Add(enhancement);
            await _db.SaveChangesAsync();
            return Ok(enhancement);
        }

        [HttpGet("{id}/enhancement")]
        public IActionResult GetLatestEnhancement(int id)
        {
            var enhancement = _db.CvEnhancements
                .Where(e => e.CvId == id)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            if (enhancement == null) return NotFound("No enhancement found.");
            return Ok(enhancement);
        }

        [HttpGet("{id}/enhancement/pdf")]
        public IActionResult DownloadEnhancedCvPdf(int id)
        {
            var enhancement = _db.CvEnhancements
                .Where(e => e.CvId == id)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            if (enhancement == null) return NotFound("No enhancement found.");
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Enhanced CV").FontSize(20).Bold();
                    page.Content().Column(col =>
                    {
                        col.Item().Text("Suggestions:").Bold();
                        col.Item().Text(enhancement.Suggestions);
                        col.Item().PaddingVertical(10);
                        col.Item().Text("Enhanced CV:").Bold();
                        col.Item().Text(enhancement.EnhancedCvText).FontFamily("Arial");
                    });
                });
            });
            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"enhanced_cv_{id}.pdf");
        }
    }
}
