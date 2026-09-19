using Microsoft.EntityFrameworkCore;

namespace SecurePortal.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<CaptchaToken> CaptchaTokens { get; set; }

        public DbSet<EncryptedRecord> EncryptedRecords { get; set; }

    }

}