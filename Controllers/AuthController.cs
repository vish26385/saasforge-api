using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs;
using SaaSForge.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SaaSForge.Api.Models.Auth;
using SaaSForge.Api.Services.Auth;
using SaaSForge.Api.Services.Common;

namespace SaaSForge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _config;
        private static readonly Dictionary<string, string> _refreshTokens = new(); // demo in-memory store
        private readonly TokenService _tokenService;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public AuthController(UserManager<ApplicationUser> userManager,
                              RoleManager<IdentityRole> roleManager,
                              IConfiguration config, 
                              TokenService tokenService, 
                              AppDbContext context,
                              IEmailService emailService,
                              IWebHostEnvironment env
                              )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
            _tokenService = tokenService;
            _context = context;
            _emailService = emailService;
            _env = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 🧠 Create the new Identity user
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName
            };

            // ✅ Save user to the Identity store
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("User"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                }

                // Assign normal user role
                await _userManager.AddToRoleAsync(user, "User");
            }

            if (!result.Succeeded)
            {
                var duplicateEmailError = result.Errors.FirstOrDefault(x =>
                    x.Code == "DuplicateUserName" ||
                    x.Description.Contains("already taken"));

                if (duplicateEmailError != null)
                {
                    return Conflict(new
                    {
                        message = "An account with this email already exists"
                    });
                }

                var firstError = result.Errors.FirstOrDefault()?.Description
                                 ?? "Something went wrong. Please try again.";

                return BadRequest(new
                {
                    message = firstError
                });
            }

            // 🧩 Generate JWT + Refresh Token for the newly registered user
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Save refresh token to DB
            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // valid for 7 days
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();

            return Ok(new { token, refreshToken });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
                return Unauthorized(new { message = "Invalid credentials" });

            // ✅ Generate tokens
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Save refresh token to DB
            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // valid for 7 days
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();


            // ✅ Return all together
            return Ok(new
            {
                token,
                refreshToken,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.UserName
                }
            });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto dto)
        {
            if (string.IsNullOrEmpty(dto.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var storedRefresh = await _context.UserRefreshTokens
                 .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (storedRefresh == null)
                return Unauthorized(new { message = "Invalid refresh token" });

            if (!storedRefresh.IsActive)
                return Unauthorized(new { message = "Token expired or revoked" });

            var user = await _userManager.FindByIdAsync(storedRefresh.UserId);
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            // 🔄 Generate new tokens
            var newJwt = await _tokenService.GenerateJwtTokenAsync(user);
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Revoke old one and add new record
            storedRefresh.RevokedAt = DateTime.UtcNow;
            var newRecord = new UserRefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(newRecord);
            await _context.SaveChangesAsync();

            return Ok(new { token = newJwt, refreshToken = newRefreshToken });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var genericMessage = "If an account exists for this email, a password reset link has been sent.";

            var email = dto.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Ok(new
                {
                    message = genericMessage
                });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return StatusCode(500, new
                {
                    message = "Client application base URL is not configured."
                });
            }

            var resetLink =
                $"{frontendBaseUrl.TrimEnd('/')}/reset-password" +
                $"?email={Uri.EscapeDataString(email)}" +
                $"&token={Uri.EscapeDataString(token)}";

            var subject = "Reset your password";
            var body = $@"
                        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #111827;'>
                            <h2 style='margin-bottom: 16px;'>Reset your password</h2>
                            <p>We received a request to reset your password.</p>
                            <p>Click the button below to set a new password:</p>
                            <p style='margin: 24px 0;'>
                                <a href='{resetLink}'
                                   style='background-color:#2563eb;color:white;padding:12px 20px;text-decoration:none;border-radius:8px;display:inline-block;'>
                                   Reset Password
                                </a>
                            </p>
                            <p>If the button does not work, copy and paste this link into your browser:</p>
                            <p><a href='{resetLink}'>{resetLink}</a></p>
                            <p>If you did not request this, you can safely ignore this email.</p>
                        </div>";

            await _emailService.SendEmailAsync(email, subject, body);

            var returnTokenInDevelopment =
                bool.TryParse(_config["PasswordReset:ReturnTokenInDevelopment"], out var flag) && flag;

            if (_env.IsDevelopment() && returnTokenInDevelopment)
            {
                return Ok(new
                {
                    message = genericMessage,
                    resetToken = token,
                    resetLink = resetLink
                });
            }

            return Ok(new
            {
                message = genericMessage
            });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!string.Equals(dto.NewPassword, dto.ConfirmPassword, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    message = "New password and confirm password do not match."
                });
            }

            var email = dto.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return BadRequest(new
                {
                    message = "Invalid reset request."
                });
            }

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Password reset failed.",
                    errors = result.Errors.Select(e => e.Description).ToList()
                });
            }

            var tokens = _context.UserRefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null);

            foreach (var t in tokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Password has been reset successfully."
            });
        }

        // ✅ GET /auth/me
        [HttpGet("me")]
        public IActionResult Me()
        {
            // Extract user data from JWT claims
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.Identity?.Name ?? "Unknown";
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var id = idClaim != null ? int.Parse(idClaim) : 0;

            return Ok(new
            {
                id,
                name,
                email
            });
        }

        [HttpGet("env-check")]
        public IActionResult GetEnvCheck()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var key = _config["Jwt:Key"]?.Substring(0, 10);
            var conn = _config.GetConnectionString("DefaultConnection");

            return Ok(new
            {
                environment = env,
                jwtKeyStart = key,
                connection = conn
            });
        }
    }
}
