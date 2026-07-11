using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

public class UptimeOcoContext(DbContextOptions<UptimeOcoContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Monitor> Monitors => Set<Monitor>();

    public DbSet<PingResult> PingResults => Set<PingResult>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Monitor>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(200);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Monitors)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PingResult>(entity =>
        {
            entity.HasIndex(p => p.CheckedAt);

            entity.HasOne(p => p.Monitor)
                .WithMany(m => m.PingResults)
                .HasForeignKey(p => p.MonitorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Incident>(entity =>
        {
            entity.HasIndex(i => i.StartedAt);

            entity.HasOne(i => i.Monitor)
                .WithMany(m => m.Incidents)
                .HasForeignKey(i => i.MonitorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationChannel>(entity =>
        {
            entity.Property(n => n.Name).HasMaxLength(200);

            entity.HasOne(n => n.User)
                .WithMany(u => u.NotificationChannels)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
