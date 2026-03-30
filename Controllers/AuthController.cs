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
using Google.Apis.Auth;

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
        private readonly GoogleTokenValidatorService _googleTokenValidatorService;

        public AuthController(UserManager<ApplicationUser> userManager,
                              RoleManager<IdentityRole> roleManager,
                              IConfiguration config, 
                              TokenService tokenService, 
                              AppDbContext context,
                              IEmailService emailService,
                              IWebHostEnvironment env,
                              GoogleTokenValidatorService googleTokenValidatorService
                              )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
            _tokenService = tokenService;
            _context = context;
            _emailService = emailService;
            _env = env;
            _googleTokenValidatorService = googleTokenValidatorService;
        }

        private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user, bool isNewUser)
        {
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                IsNewUser = isNewUser,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName
                }
            };
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 🧠 Create the new Identity user
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                EmailConfirmed = false // IMPORTANT
            };

            // ✅ Save user
            var result = await _userManager.CreateAsync(user, dto.Password);

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

            // ✅ Ensure role exists
            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Assign role
            await _userManager.AddToRoleAsync(user, "User");

            // ================================
            // 🔥 EMAIL VERIFICATION (NEW)
            // ================================
            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var frontendBaseUrl = _config["ClientApp:BaseUrl"];

            var verificationLink =
                $"{frontendBaseUrl.TrimEnd('/')}/verify-email" +
                $"?email={Uri.EscapeDataString(user.Email!)}" +
                $"&token={Uri.EscapeDataString(emailToken)}";

            var subject = "Verify your email - SaaSForge";
            var body = $@"
        <h2>Welcome to SaaSForge</h2>
        <p>Please verify your email by clicking below:</p>
        <a href='{verificationLink}'>Verify Email</a>
        <br/><br/>
        <p>If you didn't create this account, ignore this email.</p>";

            await _emailService.SendEmailAsync(user.Email!, subject, body);

            // ================================
            // 🔐 JWT + REFRESH TOKEN (UNCHANGED)
            // ================================
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();

            // ================================
            // 🧪 DEV SUPPORT (OPTIONAL)
            // ================================
            var returnTokenInDev =
                bool.TryParse(_config["EmailConfirmation:ReturnTokenInDevelopment"], out var flag) && flag;

            if (_env.IsDevelopment() && returnTokenInDev)
            {
                return Ok(new
                {
                    token,
                    refreshToken,
                    verificationToken = emailToken,
                    verificationLink
                });
            }

            // ================================
            // ✅ FINAL RESPONSE
            // ================================
            return Ok(new
            {
                token,
                refreshToken,
                message = "Registration successful. Please verify your email."
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // ❌ Invalid email OR password
            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            // 🚨 EMAIL NOT VERIFIED (PUT HERE)
            if (!user.EmailConfirmed)
            {
                return BadRequest(new
                {
                    message = "Please verify your email address before signing in."
                });
            }

            //// ✅ Generate tokens
            //var token = await _tokenService.GenerateJwtTokenAsync(user);
            //var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            //// Save refresh token to DB
            //var userRefresh = new UserRefreshToken
            //{
            //    UserId = user.Id,
            //    Token = refreshToken,
            //    ExpiresAt = DateTime.UtcNow.AddDays(7),
            //    CreatedAt = DateTime.UtcNow
            //};

            //_context.UserRefreshTokens.Add(userRefresh);
            //await _context.SaveChangesAsync();

            //// ✅ Return response
            //return Ok(new
            //{
            //    token,
            //    refreshToken,
            //    user = new
            //    {
            //        user.Id,
            //        user.Email,
            //        user.UserName
            //    }
            //});

            var authResponse = await BuildAuthResponseAsync(user, false);
            return Ok(authResponse);
        }

        [AllowAnonymous]
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            GoogleJsonWebSignature.Payload payload;

            try
            {
                payload = await _googleTokenValidatorService.ValidateAsync(dto.IdToken);
            }
            catch
            {
                return Unauthorized(new { message = "Invalid Google ID token." });
            }

            if (string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(payload.Email))
            {
                return Unauthorized(new { message = "Google account information is incomplete." });
            }

            // 1) Try by external login first
            var user = await _userManager.FindByLoginAsync("Google", payload.Subject);

            // 2) If not found, try by email
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(payload.Email);
            }

            var isNewUser = false;

            // 3) Create new local Identity user if none exists
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FullName = payload.Name ?? payload.Email,
                    EmailConfirmed = payload.EmailVerified
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "Failed to create local user for Google login.",
                        errors = createResult.Errors.Select(e => e.Description).ToList()
                    });
                }

                if (!await _roleManager.RoleExistsAsync("User"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                }

                isNewUser = true;

                await _userManager.AddToRoleAsync(user, "User");
            }
            else
            {
                // If the user already exists locally and Google says email is verified,
                // ensure local EmailConfirmed is true.
                if (!user.EmailConfirmed && payload.EmailVerified)
                {
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }
            }

            // 4) Link Google login if not already linked
            var existingLogins = await _userManager.GetLoginsAsync(user);
            var alreadyLinked = existingLogins.Any(x =>
                x.LoginProvider == "Google" && x.ProviderKey == payload.Subject);

            if (!alreadyLinked)
            {
                var addLoginResult = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo("Google", payload.Subject, "Google"));

                if (!addLoginResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "Failed to link Google login to local account.",
                        errors = addLoginResult.Errors.Select(e => e.Description).ToList()
                    });
                }
            }

            // 5) Return same auth response shape as normal login
            var authResponse = await BuildAuthResponseAsync(user, isNewUser);
            return Ok(authResponse);
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

        private async Task<(string token, string link)> GenerateEmailConfirmationLinkAsync(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                throw new InvalidOperationException("Client application base URL is not configured.");
            }

            var link =
                $"{frontendBaseUrl.TrimEnd('/')}/verify-email" +
                $"?email={Uri.EscapeDataString(user.Email ?? string.Empty)}" +
                $"&token={Uri.EscapeDataString(token)}";

            return (token, link);
        }

        private async Task SendVerificationEmailAsync(ApplicationUser user)
        {
            var (_, verificationLink) = await GenerateEmailConfirmationLinkAsync(user);

            var subject = "Verify your email address";
            var body = $@"
                        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #111827;'>
                            <h2 style='margin-bottom: 16px;'>Verify your email</h2>
                            <p>Thanks for creating your account.</p>
                            <p>Click the button below to verify your email address:</p>
                            <p style='margin: 24px 0;'>
                                <a href='{verificationLink}'
                                   style='background-color:#2563eb;color:white;padding:12px 20px;text-decoration:none;border-radius:8px;display:inline-block;'>
                                   Verify Email
                                </a>
                            </p>
                            <p>If the button does not work, copy and paste this link into your browser:</p>
                            <p><a href='{verificationLink}'>{verificationLink}</a></p>
                            <p>If you did not create this account, you can safely ignore this email.</p>
                        </div>";

            await _emailService.SendEmailAsync(user.Email!, subject, body);
        }

        [AllowAnonymous]
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var email = dto.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return BadRequest(new
                {
                    message = "Invalid verification request."
                });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new
                {
                    message = "Email is already verified."
                });
            }

            var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Email verification failed.",
                    errors = result.Errors.Select(e => e.Description).ToList()
                });
            }

            return Ok(new
            {
                message = "Email verified successfully."
            });
        }

        [AllowAnonymous]
        [HttpPost("resend-verification-email")]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var genericMessage = "If an account exists for this email, a verification link has been sent.";

            var email = dto.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Ok(new
                {
                    message = genericMessage
                });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new
                {
                    message = "Email is already verified."
                });
            }

            await SendVerificationEmailAsync(user);

            var returnTokenInDevelopment =
                bool.TryParse(_config["EmailConfirmation:ReturnTokenInDevelopment"], out var flag) && flag;

            if (_env.IsDevelopment() && returnTokenInDevelopment)
            {
                var (token, link) = await GenerateEmailConfirmationLinkAsync(user);

                return Ok(new
                {
                    message = genericMessage,
                    verificationToken = token,
                    verificationLink = link
                });
            }

            return Ok(new
            {
                message = genericMessage
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
