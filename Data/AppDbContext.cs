using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Models;
using SaaSForge.Api.Models.Auth;

namespace SaaSForge.Api.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
        public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();
        public DbSet<Business> Businesses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserDeviceToken>()
                .HasIndex(x => new { x.UserId, x.ExpoPushToken })
                .IsUnique();

            modelBuilder.Entity<Business>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                b.Property(x => x.Slug)
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property(x => x.Email)
                    .HasMaxLength(200);

                b.Property(x => x.Phone)
                    .HasMaxLength(30);

                b.Property(x => x.Address)
                    .HasMaxLength(300);

                b.Property(x => x.TimeZone)
                    .HasMaxLength(100);

                b.HasIndex(x => x.OwnerUserId)
                    .IsUnique();

                b.HasIndex(x => x.Slug)
                    .IsUnique();

                b.HasOne(x => x.OwnerUser)
                    .WithMany()
                    .HasForeignKey(x => x.OwnerUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
