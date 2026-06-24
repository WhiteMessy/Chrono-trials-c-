using Microsoft.EntityFrameworkCore;
using ChronoTrial.Models;

namespace ChronoTrial.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<Purchase> Purchases => Set<Purchase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("gebruiker");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").IsRequired();
            entity.Property(x => x.Wachtwoord).HasColumnName("wachtwoord").IsRequired();
            entity.Property(x => x.Purchased).HasColumnName("purchased").HasDefaultValue(false);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }
}
