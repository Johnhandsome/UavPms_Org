# UAV-GridGuard: Hệ thống Quản lý & Phân tích Giám sát Đường dây 110kV

Dự án này là hệ thống backend quản lý và phân tích dữ liệu kiểm tra đường dây truyền tải điện bằng thiết bị bay không người lái (UAV). Phần này tập trung vào cấu hình **Notification Service**, kết nối cơ sở dữ liệu **Supabase**, và hệ thống tác vụ chạy ngầm **Hangfire**.

---

## Hướng dẫn Khởi chạy nhanh (Quick Start)

Dự án đã được thiết lập để kết nối trực tiếp đến Cloud Database (**Supabase**) và tự động cấu hình di chuyển cơ sở dữ liệu (Database Migrations). Người mới pull dự án về **không cần cài đặt PostgreSQL** hoặc **bật Docker** cục bộ vẫn có thể khởi chạy ứng dụng ngay lập tức.

### Bước 1: Khởi chạy Web API
Mở Terminal tại thư mục gốc của dự án và chạy lệnh:
```bash
dotnet run --project UavPms.WebApi/UavPms.WebApi.csproj
```

Khi chạy ứng dụng:
* Hệ thống sẽ tự động chạy EF Migrations để đồng bộ các bảng cơ sở dữ liệu mới (bao gồm cả bảng `Notifications` và các bảng cấu trúc hệ thống).
* Khởi tạo các bảng quản lý tác vụ của Hangfire trên database Supabase.
* Máy chủ Web API sẽ lắng nghe tại cổng mặc định (ví dụ: `http://localhost:5194` - hãy kiểm tra cổng hiển thị trên Terminal của bạn).

---

## Kiểm thử Các Tính năng (Testing Guide)

### 1. Hệ thống Thông báo (Notification APIs)
Truy cập giao diện Swagger tại: `http://localhost:5194/swagger`
Bạn có thể gọi trực tiếp các HTTP Endpoint để kiểm thử luồng thông báo mà không cần có RabbitMQ:
* **Tạo thông báo mới**: `POST /api/notifications` với body chứa thông tin người nhận (`userId`), tiêu đề (`title`), và nội dung (`content`).
* **Xem lịch sử thông báo**: `GET /api/notifications/history?userId={userId}` để lấy toàn bộ danh sách thông báo của người dùng đó.
* **Đánh dấu đã đọc**: `PUT /api/notifications/{id}/read` để đánh dấu thông báo cụ thể là đã đọc.

### 2. Tác vụ Chạy ngầm (Hangfire Background Jobs)
Truy cập Hangfire Dashboard tại: `http://localhost:5194/hangfire`
Tại đây, bạn có thể theo dõi và kích hoạt thủ công các tác vụ chạy ngầm:
* **auto-cleanup-job**: Tác vụ tự động dọn dẹp các tệp tin lưu trữ tạm thời và file logs cũ hơn 30 ngày.
* **daily-summary-job**: Tác vụ tổng hợp và giả lập gửi email báo cáo sự cố hàng ngày.
* *Mẹo*: Chọn mục **Recurring Jobs** -> Chọn Job -> Nhấp **Trigger Now** để chạy thử ngay lập tức mà không cần đợi lịch hẹn.

---

## Cấu hình RabbitMQ (Tùy chọn)

Ứng dụng sử dụng RabbitMQ để truyền nhận tin nhắn bất đồng bộ khi có sự kiện `MissionCreated` (Phân công nhiệm vụ) hoặc `DefectDetected` (Phát hiện lỗi bằng AI).

### Lưu ý về cảnh báo lỗi RabbitMQ khi phát triển:
Nếu bạn không chạy RabbitMQ cục bộ, Terminal sẽ ghi log cảnh báo lỗi kết nối:
`Failed to connect or establish channel to RabbitMQ. Retrying in 5 seconds...`
> **Lưu ý**: Đây là cảnh báo kết nối thử lại tự động và **hoàn toàn không làm sập ứng dụng**. Bạn vẫn có thể kiểm thử các API khác và Hangfire bình thường.

### Cách xử lý lỗi cảnh báo:
* **Cách A (Nếu muốn chạy thử RabbitMQ)**: Hãy bật RabbitMQ cục bộ bằng Docker:
  ```bash
  docker run -d --name uav_rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management-alpine
  ```
  Hệ thống sẽ tự động nhận diện và kết nối thành công.
* **Cách B (Nếu muốn tắt hẳn cảnh báo để tập trung viết code khác)**: Mở [Program.cs](file:///home/minhchau/Documents/PMS/UavPms.WebApi/Program.cs#L30-L31) và tạm thời comment lại hai dòng đăng ký Hosted Services:
  ```csharp
  // builder.Services.AddHostedService<MissionCreatedConsumer>();
  // builder.Services.AddHostedService<DefectDetectedConsumer>();
  ```
