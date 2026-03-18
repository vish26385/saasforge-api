using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Business;
using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Business
{
    public class BusinessService : IBusinessService
    {
        private readonly AppDbContext _context;

        public BusinessService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BusinessResponseDto> CreateAsync(string ownerUserId, CreateBusinessDto dto)
        {
            var existingBusiness = await _context.Businesses
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (existingBusiness != null)
            {
                throw new InvalidOperationException("A business already exists for this user.");
            }

            var slugExists = await _context.Businesses
                .AnyAsync(x => x.Slug == dto.Slug);

            if (slugExists)
            {
                throw new InvalidOperationException("This slug is already in use.");
            }

            var nowUtc = DateTime.UtcNow;

            var business = new Models.Business
            {
                OwnerUserId = ownerUserId,
                Name = dto.Name.Trim(),
                Slug = dto.Slug.Trim().ToLower(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? null : dto.TimeZone.Trim(),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.Businesses.Add(business);
            await _context.SaveChangesAsync();

            return MapToResponse(business);
        }

        public async Task<BusinessResponseDto?> GetMyBusinessAsync(string ownerUserId)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                return null;
            }

            return MapToResponse(business);
        }

        public async Task<BusinessResponseDto?> UpdateMyBusinessAsync(string ownerUserId, UpdateBusinessDto dto)
        {
            var business = await _context.Businesses
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                return null;
            }

            var normalizedSlug = dto.Slug.Trim().ToLower();

            var slugExists = await _context.Businesses
                .AnyAsync(x => x.Slug == normalizedSlug && x.Id != business.Id);

            if (slugExists)
            {
                throw new InvalidOperationException("This slug is already in use.");
            }

            business.Name = dto.Name.Trim();
            business.Slug = normalizedSlug;
            business.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            business.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            business.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            business.TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? null : dto.TimeZone.Trim();
            business.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponse(business);
        }

        private static BusinessResponseDto MapToResponse(Models.Business business)
        {
            return new BusinessResponseDto
            {
                Id = business.Id,
                OwnerUserId = business.OwnerUserId,
                Name = business.Name,
                Slug = business.Slug,
                Email = business.Email,
                Phone = business.Phone,
                Address = business.Address,
                TimeZone = business.TimeZone,
                CreatedAtUtc = business.CreatedAtUtc,
                UpdatedAtUtc = business.UpdatedAtUtc
            };
        }
    }
}