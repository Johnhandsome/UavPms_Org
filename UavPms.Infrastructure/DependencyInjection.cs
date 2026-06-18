using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UavPms.Infrastructure.Persistence;
using UavPms.Infrastructure.Messaging;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.Infrastructure.Services;

namespace UavPms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Truyền Connection String vào cấu hình UseNpgsql
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => {
                    o.UseNetTopologySuite();
                    o.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
        });
        
        // Đăng ký RabbitMQ Connection helper dưới dạng Singleton
        services.AddSingleton<RabbitMqConnection>();

        // Đăng ký Unit of Work và Generic Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        
        // Đăng ký các Repositories đặc thù 
        services.AddScoped<ITowerRepository, TowerRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IAnomalyRepository, AnomalyRepository>();
        services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
        
        // Đăng ký Password Hasher và JWT Provider
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        
        return services;
    }
}