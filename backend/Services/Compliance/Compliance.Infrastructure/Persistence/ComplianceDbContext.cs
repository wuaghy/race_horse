using Compliance.Application.Abstractions;
using Compliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compliance.Infrastructure.Persistence;

public class ComplianceDbContext : DbContext, IComplianceDbContext
{
    public ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : base(options)
    {
    }

    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();
    public DbSet<RegulationRule> RegulationRules => Set<RegulationRule>();
    public DbSet<RegulationDocumentRequirement> RegulationDocumentRequirements => Set<RegulationDocumentRequirement>();
    public DbSet<ClearanceCase> ClearanceCases => Set<ClearanceCase>();
    public DbSet<ClearanceCaseDocument> ClearanceCaseDocuments => Set<ClearanceCaseDocument>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DocumentType
        modelBuilder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("DocumentTypes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Active).HasDefaultValue(true);
        });

        // Document
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentNo).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.DocumentNo).IsUnique();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileUrl).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.IssuingAuthority).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.CurrentReviewNote).HasMaxLength(2000);
            entity.Property(e => e.VersionNo).HasDefaultValue(1);

            entity.HasOne(e => e.DocumentType)
                .WithMany(dt => dt.Documents)
                .HasForeignKey(e => e.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.HorseId);
            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => e.TripId);
            entity.HasIndex(e => e.Status);
        });

        // DocumentReview
        modelBuilder.Entity<DocumentReview>(entity =>
        {
            entity.ToTable("DocumentReviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(2000);

            entity.HasOne(e => e.Document)
                .WithMany(d => d.Reviews)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DocumentId);
        });

        // RegulationRule
        modelBuilder.Entity<RegulationRule>(entity =>
        {
            entity.ToTable("RegulationRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleCode).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RuleCode).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasIndex(e => new { e.OriginCountryId, e.DestinationCountryId });
        });

        // RegulationDocumentRequirement
        modelBuilder.Entity<RegulationDocumentRequirement>(entity =>
        {
            entity.ToTable("RegulationDocumentRequirements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AuthorityName).HasMaxLength(255);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.RegulationRule)
                .WithMany(r => r.Requirements)
                .HasForeignKey(e => e.RegulationRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DocumentType)
                .WithMany(dt => dt.Requirements)
                .HasForeignKey(e => e.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.RegulationRuleId, e.DocumentTypeId }).IsUnique();
        });

        // ClearanceCase
        modelBuilder.Entity<ClearanceCase>(entity =>
        {
            entity.ToTable("ClearanceCases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaseNo).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.CaseNo).IsUnique();
            entity.Property(e => e.AuthorityName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.RejectionReason).HasMaxLength(2000);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.VersionNo).HasDefaultValue(1);

            entity.HasIndex(e => e.TripId);
            entity.HasIndex(e => e.Status);
        });

        // ClearanceCaseDocument
        modelBuilder.Entity<ClearanceCaseDocument>(entity =>
        {
            entity.ToTable("ClearanceCaseDocuments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();

            entity.HasOne(e => e.ClearanceCase)
                .WithMany(c => c.ClearanceCaseDocuments)
                .HasForeignKey(e => e.ClearanceCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Document)
                .WithMany(d => d.ClearanceCaseDocuments)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ClearanceCaseId, e.DocumentId }).IsUnique();
        });

        // OutboxMessages
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(150).IsRequired();
            entity.Property(e => e.AggregateType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("PENDING");

            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        // AuditLogs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OldState).HasMaxLength(50);
            entity.Property(e => e.NewState).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(2000);

            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });
    }
}
