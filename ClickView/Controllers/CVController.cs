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

            // Check if this is the user's first CV
            var isFirstCv = !await _db.CVs.AnyAsync(c => c.UserId == userId);

            var cv = new CV
            {
                UserId = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = content,
                ExtractedText = extractedText,
                IsDefault = isFirstCv // Set as default if it's the first CV
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

            return Ok(new { 
                message = "CV uploaded and text extracted.", 
                cvId = cv.CvId,
                isDefault = cv.IsDefault 
            });
        }
        private async Task<CvInsights?> ExtractInsightsFromFlask(string cvText)
        {
            try
            {
                using var httpClient = new HttpClient();
                var payload = new { cv_text = cvText };
                var flaskUrl = "http://127.0.0.1:5000/extract-keywords"; // Updated to correct endpoint

                var response = await httpClient.PostAsJsonAsync(flaskUrl, payload);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Flask API error: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                if (json == null) return null;

                return new CvInsights
                {
                    TechnicalSkills = GetStringFromJsonArray(json, "technical_skills"),
                    SoftSkills = GetStringFromJsonArray(json, "soft_skills"),
                    ToolsAndTechnologies = GetStringFromJsonArray(json, "tools_and_technologies"),
                    Certifications = GetStringFromJsonArray(json, "certifications"),
                    ExperienceSummary = json.GetValueOrDefault("experience_summary", "").ToString() ?? ""
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Flask API: {ex.Message}");
                return null;
            }
        }

        private string GetStringFromJsonArray(Dictionary<string, object> json, string key)
        {
            if (json.TryGetValue(key, out var value) && value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                var array = element.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                return string.Join(", ", array);
            }
            return "";
        }

        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserCvs(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(new { message = "Invalid user ID" });
                }

                // First check if user exists
                var userExists = await _db.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                {
                    Console.WriteLine($"User not found with ID: {userId}");
                    return NotFound(new { message = "User not found." });
                }

                // Then get CVs without including User to avoid circular references
                var cvs = await _db.CVs
                    .Where(c => c.UserId == userId)
                    .Select(c => new
                    {
                        id = c.CvId.ToString(),
                        title = Path.GetFileNameWithoutExtension(c.FileName),
                        lastModified = c.UploadedAt,
                        template = c.Template, // Use the template from the database
                        isDefault = c.IsDefault,
                        contentType = c.ContentType,
                        hasExtractedText = !string.IsNullOrWhiteSpace(c.ExtractedText)
                    })
                    .ToListAsync();

                Console.WriteLine($"Found {cvs.Count} CVs for user {userId}");
                return Ok(cvs);
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Error in GetUserCvs for user {userId}: {ex}");
                
                // Return a properly formatted JSON error response
                return StatusCode(500, new { 
                    message = "An error occurred while fetching CVs",
                    error = ex.Message
                });
            }
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
            try
            {
                var cv = await _db.CVs
                    .Include(c => c.Insights)
                    .FirstOrDefaultAsync(c => c.CvId == id);

                if (cv == null)
                {
                    return NotFound("CV not found.");
                }

                if (string.IsNullOrWhiteSpace(cv.ExtractedText))
                {
                    return BadRequest("No CV text available for enhancement. Please ensure the CV has been properly processed.");
                }

                var result = await _cvEnhancerService.EnhanceCvAsync(cv.ExtractedText, dto.JobTitle);
                
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

                return Ok(new
                {
                    suggestions = result.Suggestions,
                    enhancedCvText = result.EnhancedCv
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enhancing CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while enhancing the CV", error = ex.Message });
            }
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCv(int id)
        {
            try
            {
                var cv = await _db.CVs.FindAsync(id);
                if (cv == null)
                {
                    return NotFound(new { message = "CV not found." });
                }

                // Delete related records first
                var insights = await _db.CvInsights.Where(i => i.CvId == id).ToListAsync();
                var enhancements = await _db.CvEnhancements.Where(e => e.CvId == id).ToListAsync();

                _db.CvInsights.RemoveRange(insights);
                _db.CvEnhancements.RemoveRange(enhancements);
                _db.CVs.Remove(cv);

                await _db.SaveChangesAsync();
                return Ok(new { message = "CV deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while deleting the CV", error = ex.Message });
            }
        }

        [HttpPost("{id}/duplicate")]
        public async Task<IActionResult> DuplicateCv(int id)
        {
            try
            {
                // Find the original CV
                var originalCv = await _db.CVs
                    .Include(c => c.Insights)
                    .FirstOrDefaultAsync(c => c.CvId == id);

                if (originalCv == null)
                {
                    return NotFound("CV not found.");
                }

                // Create a new CV with copied data
                var duplicatedCv = new CV
                {
                    UserId = originalCv.UserId,
                    FileName = $"{Path.GetFileNameWithoutExtension(originalCv.FileName)} (Copy){Path.GetExtension(originalCv.FileName)}",
                    ContentType = originalCv.ContentType,
                    Content = originalCv.Content,
                    ExtractedText = originalCv.ExtractedText,
                    UploadedAt = DateTime.UtcNow
                };

                _db.CVs.Add(duplicatedCv);
                await _db.SaveChangesAsync();

                // If the original CV has insights, copy them too
                if (originalCv.Insights != null)
                {
                    var duplicatedInsights = new CvInsights
                    {
                        CvId = duplicatedCv.CvId,
                        TechnicalSkills = originalCv.Insights.TechnicalSkills,
                        SoftSkills = originalCv.Insights.SoftSkills,
                        ToolsAndTechnologies = originalCv.Insights.ToolsAndTechnologies,
                        Certifications = originalCv.Insights.Certifications,
                        ExperienceSummary = originalCv.Insights.ExperienceSummary
                    };

                    _db.CvInsights.Add(duplicatedInsights);
                    await _db.SaveChangesAsync();
                }

                // Return the duplicated CV data
                return Ok(new
                {
                    id = duplicatedCv.CvId.ToString(),
                    title = Path.GetFileNameWithoutExtension(duplicatedCv.FileName),
                    lastModified = duplicatedCv.UploadedAt,
                    template = "Modern", // Default template
                    isDefault = false, // Default value
                    contentType = duplicatedCv.ContentType,
                    hasExtractedText = !string.IsNullOrWhiteSpace(duplicatedCv.ExtractedText)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error duplicating CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while duplicating the CV", error = ex.Message });
            }
        }

        [HttpPost("{id}/set-default")]
        public async Task<IActionResult> SetDefaultCv(int id)
        {
            try
            {
                // Find the CV to set as default
                var cv = await _db.CVs.FindAsync(id);
                if (cv == null)
                {
                    return NotFound("CV not found.");
                }

                // Get all CVs for this user
                var userCvs = await _db.CVs
                    .Where(c => c.UserId == cv.UserId)
                    .ToListAsync();

                // Update all CVs to not be default
                foreach (var userCv in userCvs)
                {
                    userCv.IsDefault = false;
                }

                // Set the selected CV as default
                cv.IsDefault = true;

                await _db.SaveChangesAsync();

                return Ok(new { message = "Default CV updated successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting default CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while setting the default CV", error = ex.Message });
            }
        }

        [HttpGet("{id}/preview")]
        public async Task<IActionResult> GetCvPreview(int id)
        {
            try
            {
                var cv = await _db.CVs.FindAsync(id);
                if (cv == null)
                {
                    return NotFound("CV not found.");
                }

                // Set content disposition to inline to display in browser
                Response.Headers.Add("Content-Disposition", "inline");
                return File(cv.Content, cv.ContentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting CV preview {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while getting the CV preview", error = ex.Message });
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadCv(int id)
        {
            try
            {
                var cv = await _db.CVs.FindAsync(id);
                if (cv == null)
                {
                    return NotFound("CV not found.");
                }

                // Set content disposition to attachment to trigger download
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{cv.FileName}\"");
                return File(cv.Content, cv.ContentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while downloading the CV", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCv(int id, [FromBody] UpdateCvRequestDto dto)
        {
            try
            {
                var cv = await _db.CVs.FindAsync(id);
                if (cv == null)
                {
                    return NotFound("CV not found.");
                }

                // Update the filename to match the new title
                var extension = Path.GetExtension(cv.FileName);
                cv.FileName = $"{dto.Title}{extension}";
                cv.UploadedAt = DateTime.UtcNow; // Update the last modified time
                cv.Template = dto.Template; // Update the template

                // Save changes to the database
                await _db.SaveChangesAsync();

                // Return the updated CV data
                return Ok(new
                {
                    id = cv.CvId.ToString(),
                    title = Path.GetFileNameWithoutExtension(cv.FileName),
                    lastModified = cv.UploadedAt,
                    template = cv.Template,
                    isDefault = cv.IsDefault,
                    contentType = cv.ContentType,
                    hasExtractedText = !string.IsNullOrWhiteSpace(cv.ExtractedText)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating CV {id}: {ex}");
                return StatusCode(500, new { message = "An error occurred while updating the CV", error = ex.Message });
            }
        }
    }
}
