//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using Microsoft.IdentityModel.Tokens;
//using SaaSForge.Api.Data;
//using SaaSForge.Api.Models;
//using SaaSForge.Api.Models.Auth;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Text;
//using Task = System.Threading.Tasks.Task;

//namespace SaaSForge.Api.Services.Auth
//{
//    public class TokenService
//    {
//        private readonly IConfiguration _config;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly AppDbContext _context;

//        public TokenService(IConfiguration config, UserManager<ApplicationUser> userManager, AppDbContext context)
//        {
//            _config = config;
//            _userManager = userManager;
//            _context = context;
//        }

//        /// <summary>
//        /// Generates a JWT token for the specified user.
//        /// Includes user ID, username, and email as claims.
//        /// </summary>
//        public async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
//        {
//            var jwtSection = _config.GetSection("Jwt");
//            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
//            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//            var businessId = await _context.Businesses
//                            .Where(x => x.OwnerUserId == user.Id)
//                            .Select(x => x.Id)
//                            .FirstOrDefaultAsync();

//            var claims = new List<Claim>
//            {
//                new Claim(ClaimTypes.NameIdentifier, user.Id),
//                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
//                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
//                new Claim("id", user.Id),
//                new Claim("name", user.UserName ?? string.Empty),
//                new Claim("email", user.Email ?? string.Empty),
//                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
//            };

//            if (businessId > 0)
//            {
//                claims.Add(new Claim("businessId", businessId.ToString()));
//            }

//            var roles = await _userManager.GetRolesAsync(user);
//            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

//            var token = new JwtSecurityToken(
//                issuer: jwtSection["Issuer"],
//                audience: jwtSection["Audience"],
//                claims: claims,
//                expires: DateTime.UtcNow.AddMinutes(15),
//                signingCredentials: creds
//            );

//            return new JwtSecurityTokenHandler().WriteToken(token);
//        }        

//        /// <summary>
//        /// Generates a refresh token for the user.
//        /// </summary>
//        public async Task<string> GenerateRefreshTokenAsync()
//        {
//            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
//            return await Task.FromResult(refreshToken);
//        }
//    }
//}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SaaSForge.Api.Data;
using SaaSForge.Api.Models.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SaaSForge.Api.Services.Auth
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public TokenService(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            AppDbContext context)
        {
            _config = config;
            _userManager = userManager;
            _context = context;
        }

        //public async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        //{
        //    var jwtSection = _config.GetSection("Jwt");
        //    var key = new SymmetricSecurityKey(
        //        Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
        //    );
        //    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //    var businessId = await _context.Businesses
        //        .Where(x => x.OwnerUserId == user.Id)
        //        .Select(x => x.Id)
        //        .FirstOrDefaultAsync();

        //    var claims = new List<Claim>
        //    {
        //        new Claim(ClaimTypes.NameIdentifier, user.Id),
        //        new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
        //        new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
        //        new Claim("id", user.Id),
        //        new Claim("name", user.UserName ?? string.Empty),
        //        new Claim("email", user.Email ?? string.Empty),
        //        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        //    };

        //    if (businessId > 0)
        //    {
        //        claims.Add(new Claim("BusinessId", businessId.ToString()));
        //    }

        //    var roles = await _userManager.GetRolesAsync(user);
        //    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        //    var accessTokenMinutes =
        //        int.TryParse(_config["Jwt:AccessTokenMinutes"], out var parsedMinutes)
        //            ? parsedMinutes
        //            : 30;

        //    var token = new JwtSecurityToken(
        //        issuer: jwtSection["Issuer"],
        //        audience: jwtSection["Audience"],
        //        claims: claims,
        //        expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
        //        signingCredentials: creds
        //    );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}

        public async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var jwtSection = _config.GetSection("Jwt");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var businessId = await _context.Businesses
                .Where(x => x.OwnerUserId == user.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),

                new Claim("id", user.Id),
                new Claim("name", user.UserName ?? string.Empty),
                new Claim("email", user.Email ?? string.Empty),

                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (businessId > 0)
            {
                claims.Add(new Claim("BusinessId", businessId.ToString()));
            }

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var accessTokenMinutes =
                int.TryParse(_config["Jwt:AccessTokenMinutes"], out var parsedMinutes)
                    ? parsedMinutes
                    : 30;

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Task<string> GenerateRefreshTokenAsync()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            var refreshToken = Convert.ToBase64String(bytes);
            return Task.FromResult(refreshToken);
        }

        public int GetRefreshTokenDays()
        {
            return int.TryParse(_config["Jwt:RefreshTokenDays"], out var parsedDays)
                ? parsedDays
                : 14;
        }
    }
}
