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
using System.Net.Mail;
using System.Net;

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
        public async Task<IActionResult> Register(UserRegisterDto dto)
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

            //to generate a 5 digit code
            var verificationCode = new Random().Next(10000, 99999).ToString();
            user.EmailVerificationCode = verificationCode;
            user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);

            await _db.SaveChangesAsync();
            await SendVerificationEmail(user.Email, verificationCode);

            return Ok(new { message = "User registered successfully", userId = user.UserId });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || user.PasswordHash != HashPassword(dto.Password))
                return Unauthorized("Invalid email or password.");

            if (!user.IsEmailVerified)
            {
                return StatusCode(403, new
                {
                    message = "Email not verified. Please check your inbox.",
                    requiresVerification = true
                });
            }

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Login successful",
                accessToken,
                refreshToken,
                expiresIn = 3600
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

        [HttpPost("resend-code")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendCodeDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return NotFound("User not found.");

            if (user.IsEmailVerified)
                return BadRequest("Email already verified.");

            if (user.LastVerificationEmailSentAt.HasValue &&
                DateTime.UtcNow < user.LastVerificationEmailSentAt.Value.AddSeconds(60))
            {
                var secondsLeft = (int)(user.LastVerificationEmailSentAt.Value.AddSeconds(60) - DateTime.UtcNow).TotalSeconds;
                return StatusCode(429, $"Please wait {secondsLeft} seconds before requesting a new code.");
            }

            var newCode = new Random().Next(10000, 99999).ToString(); //5 digit code
            user.EmailVerificationCode = newCode;
            user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);
            user.LastVerificationEmailSentAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await SendVerificationEmail(user.Email, newCode);

            return Ok(new { message = "Verification code resent." });
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
        private bool SecurePasswordCompare(string hashedPassword, string providedPassword)
        {
            var hashToCompare = HashPassword(providedPassword);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hashedPassword),
                Encoding.UTF8.GetBytes(hashToCompare)
            );
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null) return NotFound("User not found.");
            if (user.IsEmailVerified) return Ok("Email already verified.");
            if (user.EmailVerificationCode != dto.Code)
                return BadRequest("Incorrect code.");
            if (user.VerificationCodeExpiry < DateTime.UtcNow)
                return BadRequest("Verification code expired.");

            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.VerificationCodeExpiry = null;

            await _db.SaveChangesAsync();
            return Ok("Email verified successfully.");
        }

        private async Task SendVerificationEmail(string email, string code)
        {
            using var smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("clickview2003@gmail.com", "iens qemx shrx pmln"),
                EnableSsl = true,
            };

            var message = new MailMessage
            {
                From = new MailAddress("clickview2003@gmail.com", "ClickView Team"),
                Subject = "ClickView Email Verification",
                Body = $"<p>Your verification code is: <strong>{code}</strong></p>",
                IsBodyHtml = true
            };

            message.To.Add(email);
            await smtp.SendMailAsync(message);
        }
    }
}
