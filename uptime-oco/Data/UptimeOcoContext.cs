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

    public DbSet<NotificationOutbox> NotificationOutboxes => Set<NotificationOutbox>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Monitor>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(200);
            entity.Property(m => m.ExpectedStatusCode).HasDefaultValue(200);
            entity.Property(m => m.TimeoutSeconds).HasDefaultValue(10);
            entity.Ignore(m => m.UptimePercent);
            entity.Ignore(m => m.Status);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Monitors)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PingResult>(entity =>
        {
            entity.Property(p => p.FailureReason).HasMaxLength(500);
            entity.HasIndex(p => p.CheckedAt);
            entity.HasIndex(p => new { p.MonitorId, p.CheckedAt });

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

        builder.Entity<NotificationOutbox>(entity =>
        {
            entity.Property(n => n.Reason).HasMaxLength(500);
            entity.Property(n => n.LastError).HasMaxLength(2000);
            entity.HasIndex(n => new { n.ProcessedAt, n.FailedAt, n.NextAttemptAt });

            entity.HasOne(n => n.Incident)
                .WithMany()
                .HasForeignKey(n => n.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.NotificationChannel)
                .WithMany()
                .HasForeignKey(n => n.NotificationChannelId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
