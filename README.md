# HeThongDatMonVaXuLyBep

Solution .NET 10 gồm project ASP.NET Core Minimal Web API, không dùng OpenAPI.

```powershell
dotnet build HeThongDatMonVaXuLyBep.slnx
dotnet run --project src/HeThongDatMonVaXuLyBep --urls http://localhost:5080
```

Endpoint mẫu: `GET http://localhost:5080/weatherforecast`.

Hai báo cáo cũ trong thư mục `output` thuộc đề tài trước, không dùng làm bài nộp cho đề tài mới.

## Console Client xem thực đơn

Chạy API trong terminal thứ nhất ở cổng 5081. Mở terminal thứ hai tại thư mục gốc:

```powershell
dotnet run --project src/HeThongDatMonVaXuLyBep.Client -- http://localhost:5081
```

Client gọi `GET /menu` và hiển thị mã, tên, nhóm món, mô tả, giá VND và trạng thái. Có thể bỏ đối số địa chỉ để dùng mặc định `http://localhost:5081`. Client thông báo khi API không kết nối được, trả mã HTTP lỗi, quá thời gian chờ hoặc trả JSON không hợp lệ.
