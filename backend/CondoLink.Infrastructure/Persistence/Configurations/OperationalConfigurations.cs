using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> b)
    { b.ToTable("worker_heartbeats"); b.HasKey(x => x.Id); b.Property(x => x.WorkerName).HasMaxLength(100); b.Property(x => x.InstanceId).HasMaxLength(100); b.Property(x => x.LastResultCode).HasMaxLength(100); b.HasIndex(x => new { x.WorkerName, x.InstanceId }).IsUnique(); }
}
public sealed class AiOperationMetricConfiguration : IEntityTypeConfiguration<AiOperationMetric>
{
    public void Configure(EntityTypeBuilder<AiOperationMetric> b)
    { b.ToTable("ai_operation_metrics"); b.HasKey(x => x.Id); b.Property(x => x.Operation).HasMaxLength(100); b.Property(x => x.Model).HasMaxLength(100); b.Property(x => x.ErrorCategory).HasMaxLength(100); b.HasIndex(x => x.Timestamp); b.HasIndex(x => new { x.Operation, x.Model, x.Timestamp }); }
}
public sealed class OperationalEventConfiguration : IEntityTypeConfiguration<OperationalEvent>
{
    public void Configure(EntityTypeBuilder<OperationalEvent> b)
    { b.ToTable("operational_events"); b.HasKey(x => x.Id); b.Property(x => x.Component).HasMaxLength(50); b.Property(x => x.Category).HasMaxLength(100); b.Property(x => x.Severity).HasMaxLength(20); b.Property(x => x.ReasonCode).HasMaxLength(100); b.Property(x => x.CorrelationId).HasMaxLength(100); b.HasIndex(x => x.Timestamp); }
}
