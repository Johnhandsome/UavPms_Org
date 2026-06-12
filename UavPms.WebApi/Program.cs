using Microsoft.Extensions.FileProviders;
using Serilog;
using UavPms.Infrastructure;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Repositories;
using UavPms.Infrastructure.Messaging;
using UavPms.WebApi.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using UavPms.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Serilog in ra Console   
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.WriteTo.Console(); // Viết log ra màn hình Terminal
});

//  ĐĂNG KÝ SERVICES VÀO DI CONTAINER
// ĐĂNG KÝ CONTROLLER
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructureServices(builder.Configuration);


builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

//Consumers
builder.Services.AddHostedService<MissionCreatedConsumer>();
builder.Services.AddHostedService<DefectDetectedConsumer>();

//Hangfire
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
});

builder.Services.AddHangfireServer();

// CẤU HÌNH CORS POLICY 
builder.Services.AddCors(options =>
{
    options.AddPolicy($"AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Chỉ cho phép URL của Frontend truy cập
            .AllowAnyMethod()                       // Cho phép mọi method  
            .AllowAnyHeader()                       // Cho phép mọi header  
            .AllowCredentials();                    // Rất quan trọng: Bắt buộc phải có để chạy SignalR sau này
    });
});

// XÂY DỰNG ỨNG DỤNG VÀ CẤU HÌNH MIDDLEWARE PIPELINE    
var app = builder.Build();

// Cấu hình môi trường Development (bật swagger)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Hangfire Dashboard (Development-only for security)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard();
}

// Tự động chạy Migration khi khởi động (dev-only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// Đăng ký các Hangfire Recurring Jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    recurringJobManager.AddOrUpdate<CleanupJob>(
        "auto-cleanup-job",
        job => job.Execute(),
        Cron.Daily);

    recurringJobManager.AddOrUpdate<DailySummaryJob>(
        "daily-summary-job",
        job => job.Execute(),
        Cron.Daily);
}

app.UseHttpsRedirection();

// Kích hoạt Middleware CORS (Bắt buộc phải đặt trước authorization)
app.UseCors("AllowFrontend");

// Cấu hình Middleware để phục vụ file tĩnh (Ảnh bằng chứng)
var imagePath = builder.Configuration["FileStorage:AlertImagesPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "uav_storage", "images");

try
{
    if (!Directory.Exists(imagePath))
    {
        Directory.CreateDirectory(imagePath);
    }
}
catch (Exception)
{
    // Fallback về thư mục cục bộ của ứng dụng nếu không có quyền truy cập
    imagePath = Path.Combine(builder.Environment.ContentRootPath, "uav_storage", "images");
    if (!Directory.Exists(imagePath))
    {
        Directory.CreateDirectory(imagePath);
    }
}

// Cấu hình map thư mục vật lý ra đường dẫn web
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagePath),
    RequestPath = "/images"
});

app.UseAuthorization();
app.MapControllers();
app.Run();  