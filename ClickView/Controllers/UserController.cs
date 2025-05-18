using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClickView.Data;
using ClickView.Models;
using ClickView.DTOs;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public UserController(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public IActionResult Register(UserRegisterDto dto)
        {
            if (_db.Users.Any(u => u.Email == dto.Email))
                return BadRequest("Email is already registered.");

            var hashedPassword = HashPassword(dto.Password);

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = hashedPassword,
                IsActive = true
            };

            _db.Users.Add(user);
            _db.SaveChanges();

            return Ok(new { message = "User registered successfully", userId = user.UserId });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || user.PasswordHash != HashPassword(dto.Password))
                return Unauthorized("Invalid email or password.");

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Login successful",
                accessToken = accessToken,
                refreshToken = refreshToken,
                expiresIn = 3600 // optional: seconds until accessToken expires
            });
        }
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return NotFound("User not found.");

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Logged out and refresh token revoked." });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return Unauthorized("Invalid or expired refresh token.");

            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim("userId", user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

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
            await _db.SaveChangesAsync();

            return Ok(new { message = "CV uploaded and text extracted.", cvId = cv.CvId });
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
        [HttpGet("{userId}/cvs")]
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
        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
       


    }
}
