using Microsoft.EntityFrameworkCore;
using UavPms.Core.Common;
using UavPms.Core.Entities;

namespace UavPms.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public DbSet<Drone> Drones => Set<Drone>();
    public DbSet<DroneTelemetry> DroneTelemetries => Set<DroneTelemetry>();
    public DbSet<DetectionAlert> DetectionAlerts => Set<DetectionAlert>();
    public DbSet<EvidenceImage> EvidenceImages => Set<EvidenceImage>();
    
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Notification> Notifications => Set<Notification>();
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}
    
    // Tự động quét và nạp các file Configuration
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
    
    // Ghi đè hàm SaveChangesAsync để tự động điền Audit Log (thời gian sửa/ tạo/...)
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // ChangeTracker sẽ lùng sục tất cả các Entity đang chuẩn bị được lưu vào DB
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                // Nếu là lệnh INSERT (Thêm mới)
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                
                // Nếu là lệnh UPDATE 
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        
        // Sau khi kiểm tra qua tất cả dữ liệu, gọi hàm gốc của EF Core để thực thi lưu xuống DB
        return base.SaveChangesAsync(cancellationToken);
    }
}