//using Google.Apis.Auth;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Cors;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SaaSForge.Api.Data;
//using SaaSForge.Api.DTOs;
//using SaaSForge.Api.Models.Auth;
//using SaaSForge.Api.Services.Auth;
//using SaaSForge.Api.Services.Common;
//using System.Security.Claims;

//namespace SaaSForge.Api.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    [EnableCors("AllowFrontend")]
//    public class AuthController : ControllerBase
//    {
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly RoleManager<IdentityRole> _roleManager;
//        private readonly IConfiguration _config;
//        private readonly TokenService _tokenService;
//        private readonly AppDbContext _context;
//        private readonly IEmailService _emailService;
//        private readonly IWebHostEnvironment _env;
//        private readonly GoogleTokenValidatorService _googleTokenValidatorService;
//        private readonly ILogger<AuthController> _logger;

//        public AuthController(
//            UserManager<ApplicationUser> userManager,
//            RoleManager<IdentityRole> roleManager,
//            IConfiguration config,
//            TokenService tokenService,
//            AppDbContext context,
//            IEmailService emailService,
//            IWebHostEnvironment env,
//            GoogleTokenValidatorService googleTokenValidatorService,
//            ILogger<AuthController> logger)
//        {
//            _userManager = userManager;
//            _roleManager = roleManager;
//            _config = config;
//            _tokenService = tokenService;
//            _context = context;
//            _emailService = emailService;
//            _env = env;
//            _googleTokenValidatorService = googleTokenValidatorService;
//            _logger = logger;
//        }

//        private async Task RevokeActiveRefreshTokensAsync(string userId)
//        {
//            var activeTokens = await _context.UserRefreshTokens
//                .Where(x => x.UserId == userId && x.RevokedAt == null)
//                .ToListAsync();

//            if (activeTokens.Count == 0)
//                return;

//            var nowUtc = DateTime.UtcNow;
//            foreach (var token in activeTokens)
//            {
//                token.RevokedAt = nowUtc;
//            }

//            await _context.SaveChangesAsync();
//        }

//        private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user, bool isNewUser)
//        {
//            await RevokeActiveRefreshTokensAsync(user.Id);

//            var token = await _tokenService.GenerateJwtTokenAsync(user);
//            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

//            var userRefresh = new UserRefreshToken
//            {
//                UserId = user.Id,
//                Token = refreshToken,
//                ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.GetRefreshTokenDays()),
//                CreatedAt = DateTime.UtcNow
//            };

//            _context.UserRefreshTokens.Add(userRefresh);
//            await _context.SaveChangesAsync();

//            return new AuthResponseDto
//            {
//                Token = token,
//                RefreshToken = refreshToken,
//                IsNewUser = isNewUser,
//                User = new AuthUserDto
//                {
//                    Id = user.Id,
//                    Email = user.Email,
//                    UserName = user.UserName
//                }
//            };
//        }

//        [HttpPost("register")]
//        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
//        {
//            var user = new ApplicationUser
//            {
//                UserName = dto.Email,
//                Email = dto.Email,
//                FullName = dto.FullName,
//                EmailConfirmed = false
//            };

//            var result = await _userManager.CreateAsync(user, dto.Password);

//            if (!result.Succeeded)
//            {
//                var duplicateEmailError = result.Errors.FirstOrDefault(x =>
//                    x.Code == "DuplicateUserName" ||
//                    x.Description.Contains("already taken"));

//                if (duplicateEmailError != null)
//                {
//                    return Conflict(new
//                    {
//                        message = "An account with this email already exists"
//                    });
//                }

//                var firstError = result.Errors.FirstOrDefault()?.Description
//                                 ?? "Something went wrong. Please try again.";

//                return BadRequest(new { message = firstError });
//            }

//            if (!await _roleManager.RoleExistsAsync("User"))
//            {
//                await _roleManager.CreateAsync(new IdentityRole("User"));
//            }

//            await _userManager.AddToRoleAsync(user, "User");

//            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

//            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
//            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
//            {
//                _logger.LogError("ClientApp:BaseUrl is not configured during registration for user {UserId}", user.Id);

//                var authResponseNoEmail = await BuildAuthResponseAsync(user, false);

//                return Ok(new
//                {
//                    token = authResponseNoEmail.Token,
//                    refreshToken = authResponseNoEmail.RefreshToken,
//                    message = "Registration successful. Verification email could not be sent right now."
//                });
//            }

//            var verificationLink =
//                $"{frontendBaseUrl.TrimEnd('/')}/verify-email" +
//                $"?email={Uri.EscapeDataString(user.Email!)}" +
//                $"&token={Uri.EscapeDataString(emailToken)}";

//            var emailSent = false;

//            try
//            {
//                emailSent = await _emailService.SendEmailVerificationAsync(
//                    user.Email!,
//                    user.FullName ?? user.Email!,
//                    verificationLink);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Verification email sending failed during registration for user {UserId}", user.Id);
//            }

//            var authResponse = await BuildAuthResponseAsync(user, false);

//            var returnTokenInDev =
//                bool.TryParse(_config["EmailConfirmation:ReturnTokenInDevelopment"], out var flag) && flag;

//            if (_env.IsDevelopment() && returnTokenInDev)
//            {
//                return Ok(new
//                {
//                    token = authResponse.Token,
//                    refreshToken = authResponse.RefreshToken,
//                    verificationToken = emailToken,
//                    verificationLink,
//                    message = emailSent
//                        ? "Registration successful. Please verify your email."
//                        : "Registration successful. Verification email could not be sent right now."
//                });
//            }

//            return Ok(new
//            {
//                token = authResponse.Token,
//                refreshToken = authResponse.RefreshToken,
//                message = emailSent
//                    ? "Registration successful. Please verify your email."
//                    : "Registration successful. Verification email could not be sent right now."
//            });
//        }

//        [HttpPost("login")]
//        public async Task<IActionResult> Login([FromBody] LoginDto dto)
//        {
//            var user = await _userManager.FindByEmailAsync(dto.Email);

//            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
//            {
//                return Unauthorized(new { message = "Invalid credentials" });
//            }

//            if (!user.EmailConfirmed)
//            {
//                return BadRequest(new
//                {
//                    message = "Please verify your email address before signing in."
//                });
//            }

//            var authResponse = await BuildAuthResponseAsync(user, false);
//            return Ok(authResponse);
//        }

//        [AllowAnonymous]
//        [HttpOptions("google-login")]
//        public IActionResult GoogleLoginOptions()
//        {
//            return Ok();
//        }

//        [AllowAnonymous]
//        [HttpPost("google-login")]
//        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            GoogleJsonWebSignature.Payload payload;

//            try
//            {
//                payload = await _googleTokenValidatorService.ValidateAsync(dto.IdToken);
//            }
//            catch
//            {
//                return Unauthorized(new { message = "Invalid Google ID token." });
//            }

//            if (string.IsNullOrWhiteSpace(payload.Subject) ||
//                string.IsNullOrWhiteSpace(payload.Email))
//            {
//                return Unauthorized(new { message = "Google account information is incomplete." });
//            }

//            var user = await _userManager.FindByLoginAsync("Google", payload.Subject);

//            if (user == null)
//            {
//                user = await _userManager.FindByEmailAsync(payload.Email);
//            }

//            var isNewUser = false;

//            if (user == null)
//            {
//                user = new ApplicationUser
//                {
//                    UserName = payload.Email,
//                    Email = payload.Email,
//                    FullName = payload.Name ?? payload.Email,
//                    EmailConfirmed = payload.EmailVerified
//                };

//                var createResult = await _userManager.CreateAsync(user);

//                if (!createResult.Succeeded)
//                {
//                    return BadRequest(new
//                    {
//                        message = "Failed to create local user for Google login.",
//                        errors = createResult.Errors.Select(e => e.Description).ToList()
//                    });
//                }

//                if (!await _roleManager.RoleExistsAsync("User"))
//                {
//                    await _roleManager.CreateAsync(new IdentityRole("User"));
//                }

//                isNewUser = true;
//                await _userManager.AddToRoleAsync(user, "User");
//            }
//            else
//            {
//                if (!user.EmailConfirmed && payload.EmailVerified)
//                {
//                    user.EmailConfirmed = true;
//                    await _userManager.UpdateAsync(user);
//                }
//            }

//            var existingLogins = await _userManager.GetLoginsAsync(user);
//            var alreadyLinked = existingLogins.Any(x =>
//                x.LoginProvider == "Google" && x.ProviderKey == payload.Subject);

//            if (!alreadyLinked)
//            {
//                var addLoginResult = await _userManager.AddLoginAsync(
//                    user,
//                    new UserLoginInfo("Google", payload.Subject, "Google"));

//                if (!addLoginResult.Succeeded)
//                {
//                    return BadRequest(new
//                    {
//                        message = "Failed to link Google login to local account.",
//                        errors = addLoginResult.Errors.Select(e => e.Description).ToList()
//                    });
//                }
//            }

//            var authResponse = await BuildAuthResponseAsync(user, isNewUser);
//            return Ok(authResponse);
//        }

//        [AllowAnonymous]
//        [HttpPost("refresh")]
//        public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto dto)
//        {
//            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
//                return BadRequest(new { message = "Refresh token is required" });

//            var storedRefresh = await _context.UserRefreshTokens
//                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

//            if (storedRefresh == null)
//                return Unauthorized(new { message = "Invalid refresh token" });

//            if (!storedRefresh.IsActive)
//                return Unauthorized(new { message = "Token expired or revoked" });

//            var user = await _userManager.FindByIdAsync(storedRefresh.UserId);
//            if (user == null)
//                return Unauthorized(new { message = "User not found" });

//            var newJwt = await _tokenService.GenerateJwtTokenAsync(user);
//            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

//            storedRefresh.RevokedAt = DateTime.UtcNow;

//            var newRecord = new UserRefreshToken
//            {
//                UserId = user.Id,
//                Token = newRefreshToken,
//                ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.GetRefreshTokenDays()),
//                CreatedAt = DateTime.UtcNow
//            };

//            _context.UserRefreshTokens.Add(newRecord);
//            await _context.SaveChangesAsync();

//            return Ok(new AuthResponseDto
//            {
//                Token = newJwt,
//                RefreshToken = newRefreshToken,
//                IsNewUser = false,
//                User = new AuthUserDto
//                {
//                    Id = user.Id,
//                    Email = user.Email,
//                    UserName = user.UserName
//                }
//            });
//        }

//        [Authorize]
//        [HttpPost("logout")]
//        public async Task<IActionResult> Logout([FromBody] TokenRefreshDto dto)
//        {
//            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
//                return Ok(new { message = "Logged out." });

//            var storedRefresh = await _context.UserRefreshTokens
//                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

//            if (storedRefresh != null && storedRefresh.RevokedAt == null)
//            {
//                storedRefresh.RevokedAt = DateTime.UtcNow;
//                await _context.SaveChangesAsync();
//            }

//            return Ok(new { message = "Logged out." });
//        }

//        [AllowAnonymous]
//        [HttpPost("forgot-password")]
//        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var genericMessage = "If an account exists for this email, a password reset link has been sent.";

//            var email = dto.Email.Trim();
//            var user = await _userManager.FindByEmailAsync(email);

//            if (user == null)
//            {
//                return Ok(new { message = genericMessage });
//            }

//            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

//            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
//            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
//            {
//                _logger.LogError("ClientApp:BaseUrl is not configured for forgot password request for user {UserId}", user.Id);
//                return Ok(new { message = genericMessage });
//            }

//            var resetLink =
//                $"{frontendBaseUrl.TrimEnd('/')}/reset-password" +
//                $"?email={Uri.EscapeDataString(email)}" +
//                $"&token={Uri.EscapeDataString(token)}";

//            try
//            {
//                await _emailService.SendPasswordResetAsync(
//                    email,
//                    user.FullName ?? user.Email ?? email,
//                    resetLink);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Password reset email sending failed for user {UserId}", user.Id);
//            }

//            var returnTokenInDevelopment =
//                bool.TryParse(_config["PasswordReset:ReturnTokenInDevelopment"], out var flag) && flag;

//            if (_env.IsDevelopment() && returnTokenInDevelopment)
//            {
//                return Ok(new
//                {
//                    message = genericMessage,
//                    resetToken = token,
//                    resetLink = resetLink
//                });
//            }

//            return Ok(new { message = genericMessage });
//        }

//        [AllowAnonymous]
//        [HttpPost("reset-password")]
//        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            if (!string.Equals(dto.NewPassword, dto.ConfirmPassword, StringComparison.Ordinal))
//            {
//                return BadRequest(new
//                {
//                    message = "New password and confirm password do not match."
//                });
//            }

//            var email = dto.Email.Trim();
//            var user = await _userManager.FindByEmailAsync(email);

//            if (user == null)
//            {
//                return BadRequest(new { message = "Invalid reset request." });
//            }

//            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

//            if (!result.Succeeded)
//            {
//                return BadRequest(new
//                {
//                    message = "Password reset failed.",
//                    errors = result.Errors.Select(e => e.Description).ToList()
//                });
//            }

//            var tokens = _context.UserRefreshTokens
//                .Where(t => t.UserId == user.Id && t.RevokedAt == null);

//            foreach (var t in tokens)
//            {
//                t.RevokedAt = DateTime.UtcNow;
//            }

//            await _context.SaveChangesAsync();

//            return Ok(new { message = "Password has been reset successfully." });
//        }

//        private async Task<(string token, string link)> GenerateEmailConfirmationLinkAsync(ApplicationUser user)
//        {
//            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

//            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
//            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
//            {
//                throw new InvalidOperationException("Client application base URL is not configured.");
//            }

//            var link =
//                $"{frontendBaseUrl.TrimEnd('/')}/verify-email" +
//                $"?email={Uri.EscapeDataString(user.Email ?? string.Empty)}" +
//                $"&token={Uri.EscapeDataString(token)}";

//            return (token, link);
//        }

//        private async Task<bool> SendVerificationEmailAsync(ApplicationUser user)
//        {
//            var (_, verificationLink) = await GenerateEmailConfirmationLinkAsync(user);

//            try
//            {
//                return await _emailService.SendEmailVerificationAsync(
//                    user.Email!,
//                    user.FullName ?? user.Email!,
//                    verificationLink);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Verification email sending failed for user {UserId}", user.Id);
//                return false;
//            }
//        }

//        [AllowAnonymous]
//        [HttpPost("confirm-email")]
//        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var email = dto.Email.Trim();
//            var user = await _userManager.FindByEmailAsync(email);

//            if (user == null)
//            {
//                return BadRequest(new { message = "Invalid verification request." });
//            }

//            if (user.EmailConfirmed)
//            {
//                return Ok(new { message = "Email is already verified." });
//            }

//            var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

//            if (!result.Succeeded)
//            {
//                return BadRequest(new
//                {
//                    message = "Email verification failed.",
//                    errors = result.Errors.Select(e => e.Description).ToList()
//                });
//            }

//            return Ok(new { message = "Email verified successfully." });
//        }

//        [AllowAnonymous]
//        [HttpPost("resend-verification-email")]
//        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailDto dto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var genericMessage = "If an account exists for this email, a verification link has been sent.";

//            var email = dto.Email.Trim();
//            var user = await _userManager.FindByEmailAsync(email);

//            if (user == null)
//            {
//                return Ok(new { message = genericMessage });
//            }

//            if (user.EmailConfirmed)
//            {
//                return Ok(new { message = "Email is already verified." });
//            }

//            try
//            {
//                await SendVerificationEmailAsync(user);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Resend verification email failed for user {UserId}", user.Id);
//            }

//            var returnTokenInDevelopment =
//                bool.TryParse(_config["EmailConfirmation:ReturnTokenInDevelopment"], out var flag) && flag;

//            if (_env.IsDevelopment() && returnTokenInDevelopment)
//            {
//                var (token, link) = await GenerateEmailConfirmationLinkAsync(user);

//                return Ok(new
//                {
//                    message = genericMessage,
//                    verificationToken = token,
//                    verificationLink = link
//                });
//            }

//            return Ok(new { message = genericMessage });
//        }

//        [Authorize]
//        [HttpGet("me")]
//        public IActionResult Me()
//        {
//            var email = User.FindFirst(ClaimTypes.Email)?.Value;
//            var name = User.Identity?.Name ?? "Unknown";
//            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

//            return Ok(new
//            {
//                id,
//                name,
//                email
//            });
//        }

//        [HttpGet("env-check")]
//        public IActionResult GetEnvCheck()
//        {
//            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
//            var key = _config["Jwt:Key"]?.Substring(0, 10);
//            var conn = _config.GetConnectionString("DefaultConnection");

//            return Ok(new
//            {
//                environment = env,
//                jwtKeyStart = key,
//                connection = conn
//            });
//        }
//    }
//}

using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs;
using SaaSForge.Api.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        private readonly TokenService _tokenService;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;
        private readonly GoogleTokenValidatorService _googleTokenValidatorService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration config,
            TokenService tokenService,
            AppDbContext context,
            IEmailService emailService,
            IWebHostEnvironment env,
            GoogleTokenValidatorService googleTokenValidatorService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
            _tokenService = tokenService;
            _context = context;
            _emailService = emailService;
            _env = env;
            _googleTokenValidatorService = googleTokenValidatorService;
            _logger = logger;
        }

        private async Task RevokeActiveRefreshTokensAsync(string userId)
        {
            var activeTokens = await _context.UserRefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ToListAsync();

            if (activeTokens.Count == 0)
                return;

            var nowUtc = DateTime.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = nowUtc;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user, bool isNewUser)
        {
            await RevokeActiveRefreshTokensAsync(user.Id);

            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.GetRefreshTokenDays()),
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
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                EmailConfirmed = false
            };

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

                return BadRequest(new { message = firstError });
            }

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }

            await _userManager.AddToRoleAsync(user, "User");

            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                _logger.LogError("ClientApp:BaseUrl is not configured during registration for user {UserId}", user.Id);

                var authResponseNoEmail = await BuildAuthResponseAsync(user, false);

                return Ok(new
                {
                    token = authResponseNoEmail.Token,
                    refreshToken = authResponseNoEmail.RefreshToken,
                    message = "Registration successful. Verification email could not be sent right now."
                });
            }

            var verificationLink =
                $"{frontendBaseUrl.TrimEnd('/')}/verify-email" +
                $"?email={Uri.EscapeDataString(user.Email!)}" +
                $"&token={Uri.EscapeDataString(emailToken)}";

            var emailSent = false;

            try
            {
                emailSent = await _emailService.SendEmailVerificationAsync(
                    user.Email!,
                    user.FullName ?? user.Email!,
                    verificationLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification email sending failed during registration for user {UserId}", user.Id);
            }            

            var authResponse = await BuildAuthResponseAsync(user, false);

            var returnTokenInDev =
                bool.TryParse(_config["EmailConfirmation:ReturnTokenInDevelopment"], out var flag) && flag;

            if (_env.IsDevelopment() && returnTokenInDev)
            {
                return Ok(new
                {
                    token = authResponse.Token,
                    refreshToken = authResponse.RefreshToken,
                    verificationToken = emailToken,
                    verificationLink,
                    message = emailSent
                        ? "Registration successful. Please verify your email."
                        : "Registration successful. Verification email could not be sent right now."
                });
            }

            return Ok(new
            {
                token = authResponse.Token,
                refreshToken = authResponse.RefreshToken,
                message = emailSent
                    ? "Registration successful. Please verify your email."
                    : "Registration successful. Verification email could not be sent right now."
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            if (!user.EmailConfirmed)
            {
                return BadRequest(new
                {
                    message = "Please verify your email address before signing in."
                });
            }

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

            var user = await _userManager.FindByLoginAsync("Google", payload.Subject);

            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(payload.Email);
            }

            var isNewUser = false;

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
                if (!user.EmailConfirmed && payload.EmailVerified)
                {
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }
            }

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

            var authResponse = await BuildAuthResponseAsync(user, isNewUser);

            // 🔥 Send welcome email ONLY for new users (Google Sign-Up)
            if (isNewUser)
            {
                try
                {
                    var frontendBaseUrl = _config["ClientApp:BaseUrl"];

                    if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                    {
                        await _emailService.SendWelcomeEmailAsync(
                            user.Email!,
                            user.FullName ?? user.Email!,
                            $"{frontendBaseUrl.TrimEnd('/')}/login");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Welcome email failed for Google signup user {UserId}", user.Id);
                }
            }

            return Ok(authResponse);
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
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

            var newJwt = await _tokenService.GenerateJwtTokenAsync(user);
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

            storedRefresh.RevokedAt = DateTime.UtcNow;

            var newRecord = new UserRefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.GetRefreshTokenDays()),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(newRecord);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Token = newJwt,
                RefreshToken = newRefreshToken,
                IsNewUser = false,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName
                }
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] TokenRefreshDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
                return Ok(new { message = "Logged out." });

            var storedRefresh = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (storedRefresh != null && storedRefresh.RevokedAt == null)
            {
                storedRefresh.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out." });
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
                return Ok(new { message = genericMessage });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var frontendBaseUrl = _config["ClientApp:BaseUrl"];
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                _logger.LogError("ClientApp:BaseUrl is not configured for forgot password request for user {UserId}", user.Id);
                return Ok(new { message = genericMessage });
            }

            var resetLink =
                $"{frontendBaseUrl.TrimEnd('/')}/reset-password" +
                $"?email={Uri.EscapeDataString(email)}" +
                $"&token={Uri.EscapeDataString(token)}";

            try
            {
                await _emailService.SendPasswordResetAsync(
                    email,
                    user.FullName ?? user.Email ?? email,
                    resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset email sending failed for user {UserId}", user.Id);
            }

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

            return Ok(new { message = genericMessage });
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
                return BadRequest(new { message = "Invalid reset request." });
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

            // 🔥 Send password reset success email
            try
            {
                var frontendBaseUrl = _config["ClientApp:BaseUrl"];

                if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                {
                    await _emailService.SendPasswordResetSuccessAsync(
                        user.Email!,
                        user.FullName ?? user.Email!,
                        $"{frontendBaseUrl.TrimEnd('/')}/login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset success email failed for user {UserId}", user.Id);
            }

            return Ok(new { message = "Password has been reset successfully." });
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

        private async Task<bool> SendVerificationEmailAsync(ApplicationUser user)
        {
            var (_, verificationLink) = await GenerateEmailConfirmationLinkAsync(user);

            try
            {
                return await _emailService.SendEmailVerificationAsync(
                    user.Email!,
                    user.FullName ?? user.Email!,
                    verificationLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification email sending failed for user {UserId}", user.Id);
                return false;
            }
        }

        //[AllowAnonymous]
        //[HttpPost("confirm-email")]
        //public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    var email = dto.Email.Trim();
        //    var user = await _userManager.FindByEmailAsync(email);

        //    if (user == null)
        //    {
        //        return BadRequest(new { message = "Invalid verification request." });
        //    }

        //    if (user.EmailConfirmed)
        //    {
        //        return Ok(new { message = "Email is already verified." });
        //    }

        //    var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

        //    if (!result.Succeeded)
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Email verification failed.",
        //            errors = result.Errors.Select(e => e.Description).ToList()
        //        });
        //    }

        //    return Ok(new { message = "Email verified successfully." });
        //}

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
                return BadRequest(new { message = "Invalid verification request." });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new { message = "Email is already verified." });
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

            // ✅ SEND WELCOME EMAIL (AFTER SUCCESSFUL VERIFICATION)
            try
            {
                var frontendBaseUrl = _config["ClientApp:BaseUrl"]?.TrimEnd('/');

                if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                {
                    await _emailService.SendWelcomeEmailAsync(
                        user.Email!,
                        user.FullName ?? user.Email!,
                        $"{frontendBaseUrl}/login"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Welcome email sending failed after email verification for user {UserId}",
                    user.Id);
            }

            return Ok(new { message = "Email verified successfully." });
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
                return Ok(new { message = genericMessage });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new { message = "Email is already verified." });
            }

            try
            {
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend verification email failed for user {UserId}", user.Id);
            }

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

            return Ok(new { message = genericMessage });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.Identity?.Name ?? "Unknown";
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

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