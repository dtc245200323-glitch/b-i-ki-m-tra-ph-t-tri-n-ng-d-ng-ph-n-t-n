# SUPPORTLY

Phần mềm chat hỗ trợ trực tuyến theo đề tài 16 môn Phát triển ứng dụng phân tán. Có đăng ký, đăng nhập, tổng quan, chat thời gian thực, lịch sử và quản lý người dùng.

## Chạy phần mềm

Giải nén `output/Supportly-App.zip`. Máy cần .NET 10 ASP.NET Core Runtime hoặc SDK và Docker Desktop. Mở Docker Desktop, chạy `START-SUPPORTLY.cmd`, trình duyệt mở http://localhost:5080. `STOP-SUPPORTLY.cmd` dừng API và Worker do bộ khởi chạy tạo. Dữ liệu nằm trong thư mục `data` của bộ phần mềm.

| Vai trò | Email | Mật khẩu thực hành |
| --- | --- | --- |
| Khách hàng | customer@supportly.local | Supportly123! |
| Tư vấn viên | agent@supportly.local | Supportly123! |
| Quản trị viên | admin@supportly.local | Supportly123! |

Tài khoản mẫu chỉ tạo khi bật SeedDemo. Dùng cửa sổ hoặc hồ sơ trình duyệt riêng cho từng vai trò. Token lưu trong sessionStorage của từng tab.

## Chạy mã nguồn

Yêu cầu .NET SDK 10 và Docker Engine. Từ thư mục gốc:

```powershell
dotnet restore Supportly.slnx --configfile NuGet.Config
dotnet build Supportly.slnx --no-restore
docker compose up -d rabbitmq
$env:DataPath = Join-Path $PWD 'data/supportly.db'
$env:SeedDemo = 'true'
dotnet run --project src/Supportly.Api --urls http://localhost:5080
```

Mở terminal thứ hai tại cùng thư mục gốc:

```powershell
$env:DataPath = Join-Path $PWD 'data/supportly.db'
dotnet run --project src/Supportly.Worker --urls http://localhost:5081
```

Hoặc chạy toàn bộ bằng `docker compose up --build -d`. Dừng bản local trước để tránh trùng cổng. Container dùng volume dữ liệu riêng.

API health: http://localhost:5080/health. Worker health: http://localhost:5081/health (503 khi chưa kết nối broker). RabbitMQ Management: http://localhost:15672, `supportly` / `supportly-dev`. Cấu hình mẫu chỉ dành cho thực hành cục bộ; triển khai thật cần HTTPS, bí mật riêng và tắt SeedDemo.

## Demo

1. Khách hàng → Trò chuyện → Tạo phiên hỗ trợ → nhập chủ đề → gửi tin.
2. Tư vấn viên → Phiên hỗ trợ → chọn phiên Waiting → Nhận phiên.
3. Hai bên trao đổi, sau đó Đóng phiên. Phiên chuyển sang chỉ đọc.
4. Lịch sử hội thoại hiển thị bản lưu do History Worker xử lý.
5. Quản trị viên quản lý tài khoản và xem số sự kiện chờ đồng bộ.

## Kiến trúc

`Supportly.Api` phục vụ giao diện, REST và SignalR `/hubs/chat`. `Supportly.Core` chứa hợp đồng, Store SQLite và cấu hình RabbitMQ. `Supportly.Worker` chạy độc lập, lưu lịch sử từ queue.

API ghi tin và outbox trong cùng transaction, sau đó phát realtime. Publisher nền dùng topic exchange `supportly.events`, queue durable `supportly.history`, publisher confirm và tin persistent. Worker ack sau commit, khử trùng bằng khóa EventId trong database. JSON lỗi hoặc version không hỗ trợ vào `supportly.dead`.

Hai tiến trình dùng chung file SQLite WAL trên cùng máy hoặc volume local. Khi mở rộng nhiều máy cần database server và SignalR backplane. Không đặt file SQLite trên ổ mạng. Chưa có tệp đính kèm, khôi phục mật khẩu qua email, chỉnh sửa vai trò tùy biến hay kiểm thử tải lớn.

## API

| Giao diện | Chức năng |
| --- | --- |
| POST /auth/register, POST /auth/login, GET /auth/me, POST /auth/logout | Tài khoản và phiên đăng nhập |
| POST /conversations, GET /conversations | Tạo và liệt kê phiên |
| GET /conversations/{id} | Chi tiết |
| GET /conversations/{id}/messages | Tin đã lưu để phục hồi sau reconnect |
| POST /conversations/{id}/accept | Tư vấn viên nhận phiên |
| POST /conversations/{id}/close | Đóng phiên và tạo sự kiện |
| GET /conversations/{id}/history | Lịch sử do Worker lưu |
| GET, POST /admin/users; POST /admin/users/{id}/toggle | Quản lý người dùng |
| GET /admin/metrics | Theo dõi outbox và lịch sử |
| Hub JoinConversation, LeaveConversation, SendMessage | Nhóm SignalR và gửi tin |

API nghiệp vụ yêu cầu `Authorization: Bearer <token>`. Hub dùng accessTokenFactory. Tự đăng ký luôn là Customer; chỉ Admin tạo Agent. Mật khẩu dùng PBKDF2 với salt ngẫu nhiên, token hết hạn sau 12 giờ. Khóa tài khoản và logout thu hồi khả năng truy cập. SendMessage nhận `(conversationId, text, clientId)`; clientId là GUID để chống lưu trùng.

## Kiểm thử

Chạy khi API, Worker và RabbitMQ đã sẵn sàng:

```powershell
node tests/integration.mjs
npm.cmd install
node tests/browser.mjs
node tests/resilience.mjs
```

Test trình duyệt dùng Chrome. Test resilience chủ động dừng/khởi động broker của dự án Compose, chỉ dùng ở môi trường thực hành. Test tạo dữ liệu thật; dùng database riêng nếu cần giữ dữ liệu chính sạch. Kết quả và ảnh ở `output/evidence`. Đã đạt 20 ca tích hợp, 4 ca phục hồi và 7 kiểm tra trình duyệt. Chưa kiểm chứng tải lớn hoặc toàn bộ image API/Worker trong Docker.

## Bàn giao

Báo cáo PDF và bản nội dung Markdown nằm trong `output`, lịch 7 tuần từ 18/11/2026 đến 05/01/2027 với đúng 6 luồng. Kế hoạch dự kiến được phân biệt với kết quả kiểm thử đã thực hiện. `Supportly-Source.zip` chứa mã nguồn, hướng dẫn và báo cáo; không chứa cơ sở dữ liệu thực hành, token, bin/obj hay bộ nhớ đệm. `Supportly-App.zip` chứa hai ứng dụng Windows framework-dependent và bộ khởi chạy.
