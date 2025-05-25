using Microsoft.AspNetCore.Mvc;
using ClickView.Data;
using ClickView.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using System.Text;

namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CVController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CVController(ApplicationDbContext db)
        {
            _db = db;
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

            var cv = new CV
            {
                UserId = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = content,
                ExtractedText = extractedText
            };

            _db.CVs.Add(cv);
            await _db.SaveChangesAsync();

            return Ok(new { message = "CV uploaded and text extracted.", cvId = cv.CvId });
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
    }
}
