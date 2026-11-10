using IntelligentMonitoring.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelligentMonitoring.Infrastructure.Persistence;

public sealed class MonitoringDbContext(
    DbContextOptions<MonitoringDbContext> options)
    : DbContext(options)
{
    public DbSet<IntegrationFailureEntity> IntegrationFailures =>
        Set<IntegrationFailureEntity>();

    public DbSet<FailureInvestigationEntity> FailureInvestigations =>
        Set<FailureInvestigationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IntegrationFailureEntity>(entity =>
        {
            entity.ToTable("IntegrationFailures");

            entity.HasKey(x => x.EventId);

            entity.Property(x => x.ServiceName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Environment)
                .HasMaxLength(50);

            entity.Property(x => x.IntegrationType)
                .HasMaxLength(100);

            entity.Property(x => x.TransactionId)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.TraceId)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.ErrorCode)
                .HasMaxLength(150);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            entity.HasIndex(x => x.TransactionId);
            entity.HasIndex(x => x.TraceId);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.ServiceName);
        });

        modelBuilder.Entity<FailureInvestigationEntity>(entity =>
        {
            entity.ToTable("FailureInvestigations");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.EventId)
                .IsUnique();

            entity.Property(x => x.Status)
                .HasMaxLength(50);

            entity.Property(x => x.FailureServiceName)
                .HasMaxLength(150);

            entity.Property(x => x.ErrorCode)
                .HasMaxLength(150);

            entity.Property(x => x.FailureMessage)
                .HasMaxLength(2000);

            entity.Property(x => x.ServicesInvolvedJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.TimelineJson)
                .HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Failure)
                .WithOne(x => x.Investigation)
                .HasForeignKey<FailureInvestigationEntity>(
                    x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}