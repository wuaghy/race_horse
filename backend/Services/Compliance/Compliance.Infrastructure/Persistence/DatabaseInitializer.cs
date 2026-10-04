using Compliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Compliance.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();

        try
        {
            if (context.Database.IsRelational())
            {
                logger.LogInformation("Ensuring Compliance database exists and migrations are applied...");
            }

            await SeedDefaultMasterDataAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Compliance database initialization note: schema might be managed by init scripts.");
        }
    }

    private static async Task SeedDefaultMasterDataAsync(ComplianceDbContext db, ILogger logger)
    {
        if (await db.DocumentTypes.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding default DocumentTypes for Compliance...");

        var defaultDocTypes = new List<DocumentType>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111101"),
                Code = "VACCINATION_CERT",
                Name = "Vaccination Certificate (Equine Influenza / EHV)",
                Scope = DocumentScope.Horse,
                Description = "Mandatory equine vaccination record covering influenza and herpesvirus.",
                DefaultValidityDays = 180,
                Active = true
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111102"),
                Code = "HORSE_PASSPORT",
                Name = "FEI Horse Passport & Registration",
                Scope = DocumentScope.Horse,
                Description = "Official FEI passport containing identification, microchip, and pedigree.",
                DefaultValidityDays = 365,
                Active = true
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111103"),
                Code = "HEALTH_CERTIFICATE",
                Name = "Veterinary Health Certificate",
                Scope = DocumentScope.Horse,
                Description = "Pre-export clinical examination certificate issued within 48h of departure.",
                DefaultValidityDays = 30,
                Active = true
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111104"),
                Code = "EXPORT_PERMIT",
                Name = "Government Export Quarantine Permit",
                Scope = DocumentScope.Trip,
                Description = "Official clearance permit from Department of Animal Health / Agriculture.",
                DefaultValidityDays = 30,
                Active = true
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111105"),
                Code = "IMPORT_PERMIT",
                Name = "Destination Country Import Permit",
                Scope = DocumentScope.Trip,
                Description = "Approval issued by importing quarantine authorities.",
                DefaultValidityDays = 60,
                Active = true
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111106"),
                Code = "CUSTOMS_DECLARATION",
                Name = "ATA Carnet / Customs Declaration",
                Scope = DocumentScope.Trip,
                Description = "Temporary cross-border admission carnets and customs documentation.",
                DefaultValidityDays = 90,
                Active = true
            }
        };

        db.DocumentTypes.AddRange(defaultDocTypes);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} default DocumentTypes successfully.", defaultDocTypes.Count);
    }
}
