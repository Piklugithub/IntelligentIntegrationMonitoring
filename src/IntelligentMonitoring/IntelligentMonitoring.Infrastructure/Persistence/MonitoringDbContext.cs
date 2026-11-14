
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

    public DbSet<NotificationDeliveryEntity> NotificationDeliveries =>
        Set<NotificationDeliveryEntity>();

    public DbSet<NotificationOutboxEntity> NotificationOutbox =>
    Set<NotificationOutboxEntity>();

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

            entity.Property(x => x.ProcessingLeaseExpiresAtUtc);
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

            // AI analysis configuration
            entity.Property(x => x.AiSummary)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiFailureStage)
                .HasMaxLength(250);

            entity.Property(x => x.AiProbableRootCause)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiRootCauseConfidence)
                .HasMaxLength(30);

            entity.Property(x => x.AiBusinessImpact)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiSupportingEvidenceJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiRecommendedActionsJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiInformationGapsJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.AiAnalysisStatus)
                .HasMaxLength(50);

            entity.Property(x => x.AiModelName)
                .HasMaxLength(100);

            entity.HasOne(x => x.Failure)
                .WithOne(x => x.Investigation)
                .HasForeignKey<FailureInvestigationEntity>(
                    x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Notification delivery configuration
        modelBuilder.Entity<NotificationDeliveryEntity>(entity =>
        {
            entity.ToTable("NotificationDeliveries");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(300)
                .IsRequired();

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.Property(x => x.Channel)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Destination)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.PayloadJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.ExternalMessageId)
                .HasMaxLength(500);

            entity.Property(x => x.LastError)
                .HasMaxLength(4000);

            entity.HasIndex(x => x.EventId);

            entity.HasIndex(x => new
            {
                x.Status,
                x.NextAttemptAtUtc
            });
        });


        modelBuilder.Entity<NotificationOutboxEntity>(entity =>
        {
            entity.ToTable("NotificationOutbox");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(250)
                .IsRequired();

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.Property(x => x.PayloadJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.LastError)
                .HasMaxLength(4000);

            entity.HasIndex(x => new
            {
                x.Status,
                x.NextAttemptAtUtc
            });

            entity.HasIndex(x => x.EventId);
        });

    }
}
