using HeThongDatMonVaXuLyBep.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

// Dữ liệu thực đơn mẫu được tạo một lần khi ứng dụng khởi động.
// Hiện tại danh sách chỉ nằm trong bộ nhớ, chưa được đọc từ cơ sở dữ liệu.
// Giá món ăn sử dụng đơn vị đồng Việt Nam (VND); hậu tố m tạo giá trị decimal.
// Endpoint bên dưới chỉ đọc danh sách, không thêm, sửa hoặc xóa món ăn.
var menu = new MenuItem[]
{
    new(1, "Phở bò", "Phở nước dùng bò, thịt bò và rau thơm.", "Món chính", 55000m, true),
    new(2, "Cơm gà", "Cơm ăn kèm gà, dưa leo và nước chấm.", "Món chính", 45000m, true),
    new(3, "Bún chả", "Bún ăn kèm chả nướng, rau sống và nước chấm.", "Món chính", 50000m, true),
    new(4, "Gỏi cuốn", "Một phần gồm hai cuốn tôm thịt và nước chấm.", "Món khai vị", 30000m, true),
    new(5, "Trà đào", "Trà đào dùng lạnh, có miếng đào.", "Đồ uống", 25000m, true),
    new(6, "Nước cam", "Nước cam vắt tươi.", "Đồ uống", 30000m, true)
};

// Đăng ký endpoint Minimal API cho chức năng xem danh sách món ăn.
// - Phương thức HTTP: GET, vì yêu cầu chỉ lấy dữ liệu, không thay đổi dữ liệu.
// - Đường dẫn: /menu, ví dụ http://localhost:5081/menu.
// - Đầu vào: không yêu cầu request body hoặc tham số.
// - Đầu ra: HTTP 200 OK và một mảng JSON chứa toàn bộ các món trong menu.
// Results.Ok(...) để ASP.NET Core tự chuyển các đối tượng C# thành JSON.
// Theo cấu hình mặc định, tên thuộc tính JSON dùng camelCase: id, name, price...
app.MapGet("/menu", () =>
{
    // Trả danh sách mẫu cho trình duyệt hoặc Console Client gọi API.
    // Nếu danh sách không có món nào, kết quả vẫn là 200 OK với mảng rỗng [].
    return Results.Ok(menu);
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
