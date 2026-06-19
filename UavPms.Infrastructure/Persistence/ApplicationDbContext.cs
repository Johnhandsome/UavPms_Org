using Microsoft.EntityFrameworkCore;
using UavPms.Core.Common;
using UavPms.Core.Entities;

namespace UavPms.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Substation> Substations => Set<Substation>();
    public DbSet<TransmissionLine> TransmissionLines => Set<TransmissionLine>();
    public DbSet<Tower> Towers => Set<Tower>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetHealthHistory> AssetHealthHistories => Set<AssetHealthHistory>();
    public DbSet<Uav> Uavs => Set<Uav>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionTargetLine> MissionTargetLines => Set<MissionTargetLine>();
    public DbSet<MissionFlightLog> MissionFlightLogs => Set<MissionFlightLog>();
    public DbSet<InspectionMedia> InspectionMedia => Set<InspectionMedia>();
    public DbSet<DefectCategory> DefectCategories => Set<DefectCategory>();
    public DbSet<DetectedAnomaly> DetectedAnomalies => Set<DetectedAnomaly>();
    public DbSet<EmergencyAlert> EmergencyAlerts => Set<EmergencyAlert>();
    public DbSet<AlertEscalation> AlertEscalations => Set<AlertEscalation>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<MaintenanceProof> MaintenanceProofs => Set<MaintenanceProof>();
    public DbSet<MaterialLog> MaterialLogs => Set<MaterialLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply Global Soft Delete Query Filter for BaseEntity descendants
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter((dynamic)CreateFilterExpression(entityType.ClrType));
            }
        }
    }

    private static object CreateFilterExpression(Type type)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(type, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var notExpression = System.Linq.Expressions.Expression.Not(property);
        return System.Linq.Expressions.Expression.Lambda(notExpression, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}