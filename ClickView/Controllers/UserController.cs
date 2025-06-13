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
using Microsoft.AspNetCore.Http;
using System.IO;
using Google.Apis.Auth;

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

            // Get the JWT expiry time from configuration
            var jwtSettings = _configuration.GetSection("Jwt");
            var expiresInMinutes = double.Parse(jwtSettings["ExpireMinutes"]);

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken,
                expiresIn = expiresInMinutes * 60 // Convert minutes to seconds
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

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            if (!SecurePasswordCompare(user.PasswordHash, dto.OldPassword))
                return BadRequest("Old password is incorrect.");

            user.PasswordHash = HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok("Password changed successfully.");
        }

        [Authorize]
        [HttpPatch("update-profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.ProfessionalTitle = dto.ProfessionalTitle ?? user.ProfessionalTitle;

            await _db.SaveChangesAsync();
            return Ok("Profile updated successfully.");
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return NotFound("User not found.");

            return Ok(new
            {
                id = user.UserId,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                professionalTitle = user.ProfessionalTitle,
                profilePicture = user.ProfilePictureUrl
            });
        }

        [Authorize]
        [HttpPost("upload-profile-picture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded.");

                if (!file.ContentType.StartsWith("image/"))
                    return BadRequest("File must be an image.");

                var userId = int.Parse(User.FindFirstValue("userId")!);
                var user = await _db.Users.FindAsync(userId);

                if (user == null)
                    return NotFound("User not found.");

                // Create a unique filename
                var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
                var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var profilePicturesPath = Path.Combine(wwwrootPath, "profile-pictures");
                var filePath = Path.Combine(profilePicturesPath, fileName);

                // Ensure directory exists
                Directory.CreateDirectory(profilePicturesPath);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update user's profile picture URL
                user.ProfilePictureUrl = $"/profile-pictures/{fileName}";
                await _db.SaveChangesAsync();

                return Ok(new { profilePictureUrl = user.ProfilePictureUrl });
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error uploading profile picture: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [Authorize]
        [HttpPatch("update-privacy")]
        public async Task<IActionResult> UpdatePrivacySettings(PrivacySettingsDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            user.ShowProfile = dto.ShowProfile;
            user.ShowActivity = dto.ShowActivity;
            user.ShowProgress = dto.ShowProgress;

            await _db.SaveChangesAsync();
            return Ok("Privacy settings updated successfully.");
        }

        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            // Delete user's profile picture if exists
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var filePath = Path.Combine(wwwrootPath, user.ProfilePictureUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return Ok("Account deleted successfully.");
        }

        [Authorize]
        [HttpGet("privacy-settings")]
        public async Task<IActionResult> GetPrivacySettings()
        {
            var userId = int.Parse(User.FindFirstValue("userId")!);
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            return Ok(new
            {
                showProfile = user.ShowProfile,
                showActivity = user.ShowActivity,
                showProgress = user.ShowProgress
            });
        }

        [HttpPost("google-auth")]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Credential))
                {
                    Console.WriteLine("Google auth failed: Missing credential");
                    return BadRequest(new { message = "Missing Google credential" });
                }

                Console.WriteLine($"Received Google credential: {dto.Credential.Substring(0, 20)}...");

                GoogleJsonWebSignature.Payload payload;
                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(dto.Credential);
                    Console.WriteLine($"Successfully validated Google token for email: {payload.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Google token validation failed: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return Unauthorized(new { message = "Invalid Google token", details = ex.Message });
                }

                // Check if email exists
                var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                if (existingUser != null)
                {
                    Console.WriteLine($"Found existing user with email: {payload.Email}");
                    // User exists, generate tokens
                    var accessToken = GenerateJwtToken(existingUser);
                    var refreshToken = GenerateRefreshToken();
                    
                    existingUser.RefreshToken = refreshToken;
                    existingUser.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        token = accessToken,
                        refreshToken,
                        expiresIn = 3600,
                        message = "Login successful"
                    });
                }

                Console.WriteLine($"Creating new user for email: {payload.Email}");
                // Generate a secure random password for Google users
                var randomPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                var hashedPassword = HashPassword(randomPassword);

                // Create new user
                var newUser = new User
                {
                    FirstName = payload.GivenName ?? "",
                    LastName = payload.FamilyName ?? "",
                    Email = payload.Email,
                    PasswordHash = hashedPassword,
                    IsActive = true,
                    IsEmailVerified = true, // Google emails are verified
                    ShowProfile = true,
                    ShowActivity = true,
                    ShowProgress = true
                };

                try
                {
                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Successfully created new user with ID: {newUser.UserId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create new user: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }

                // Generate tokens for new user
                var newAccessToken = GenerateJwtToken(newUser);
                var newRefreshToken = GenerateRefreshToken();
                
                newUser.RefreshToken = newRefreshToken;
                newUser.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    token = newAccessToken,
                    refreshToken = newRefreshToken,
                    expiresIn = 3600,
                    message = "Registration successful"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in Google auth: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred during Google authentication", details = ex.Message });
            }
        }
    }
}
