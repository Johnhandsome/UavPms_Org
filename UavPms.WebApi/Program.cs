using Microsoft.Extensions.FileProviders;
using Serilog;

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

app.UseHttpsRedirection();

// Kích hoạt Middleware CORS (Bắt buộc phải đặt trước authorization)
app.UseCors("AllowFrontend");

// Cấu hình Middleware để phục vụ file tĩnh (Ảnh bằng chứng)
var imagePath = "/home/an/uav_storage/images";

// Đảm bảo rằng thư mục vật lý luôn tồn tại để tránh lỗi khi chạy app
if (!Directory.Exists(imagePath))
{
    Directory.CreateDirectory(imagePath);
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