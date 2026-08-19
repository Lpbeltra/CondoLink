using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Condominium> Condominiums => Set<Condominium>();
    public DbSet<ManagementCompany> ManagementCompanies =>
        Set<ManagementCompany>();
    public DbSet<ManagementCompanyEmployee> ManagementCompanyEmployees =>
        Set<ManagementCompanyEmployee>();
    public DbSet<ManagementCompanyRequestCategory>
        ManagementCompanyRequestCategories =>
        Set<ManagementCompanyRequestCategory>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<CondominiumBlock> CondominiumBlocks => Set<CondominiumBlock>();
    public DbSet<CondominiumMembership> CondominiumMemberships =>
        Set<CondominiumMembership>();
    public DbSet<CondominiumMembershipRole> CondominiumMembershipRoles =>
        Set<CondominiumMembershipRole>();
    public DbSet<UnitMembership> UnitMemberships => Set<UnitMembership>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DomainRequest> Requests => Set<DomainRequest>();
    public DbSet<RequestStatusHistory> RequestStatusHistories =>
        Set<RequestStatusHistory>();
    public DbSet<RequestMessage> RequestMessages => Set<RequestMessage>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<RequestAiAnalysis> RequestAiAnalyses => Set<RequestAiAnalysis>();
    public DbSet<RequestResidentReplyRequirement> RequestResidentReplyRequirements =>
        Set<RequestResidentReplyRequirement>();
    public DbSet<RequestClosureConfirmation> RequestClosureConfirmations =>
        Set<RequestClosureConfirmation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WhatsAppInboundMessage> WhatsAppInboundMessages =>
        Set<WhatsAppInboundMessage>();
    public DbSet<WhatsAppSession> WhatsAppSessions => Set<WhatsAppSession>();
    public DbSet<WhatsAppDraftAttachment> WhatsAppDraftAttachments =>
        Set<WhatsAppDraftAttachment>();
    public DbSet<WhatsAppOutboundMessage> WhatsAppOutboundMessages =>
        Set<WhatsAppOutboundMessage>();
    public DbSet<WhatsAppPhoneVerification> WhatsAppPhoneVerifications =>
        Set<WhatsAppPhoneVerification>();
    public DbSet<CondominiumDocument> CondominiumDocuments => Set<CondominiumDocument>();
    public DbSet<CondominiumDocumentChunk> CondominiumDocumentChunks => Set<CondominiumDocumentChunk>();
    public DbSet<CondominiumDocumentKnowledge> CondominiumDocumentKnowledge => Set<CondominiumDocumentKnowledge>();
    public DbSet<CondominiumAssistantConversation> CondominiumAssistantConversations => Set<CondominiumAssistantConversation>();
    public DbSet<CondominiumAssistantMessage> CondominiumAssistantMessages => Set<CondominiumAssistantMessage>();
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();
    public DbSet<AiOperationMetric> AiOperationMetrics => Set<AiOperationMetric>();
    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

}
