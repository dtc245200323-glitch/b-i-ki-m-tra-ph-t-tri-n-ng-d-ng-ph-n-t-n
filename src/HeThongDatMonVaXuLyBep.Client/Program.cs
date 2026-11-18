using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HeThongDatMonVaXuLyBep.Client.Models;

// Hiển thị tiếng Việt đúng trên terminal hỗ trợ UTF-8.
Console.OutputEncoding = Encoding.UTF8;

// Nhận địa chỉ API từ đối số; mặc định dùng cổng 5081.
// Ví dụ: dotnet run --project src/HeThongDatMonVaXuLyBep.Client -- http://localhost:5081
var address = args.Length > 0 ? args[0] : "http://localhost:5081";
if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri)
    || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
{
    Console.Error.WriteLine("Địa chỉ API không hợp lệ. Ví dụ: http://localhost:5081");
    return 1;
}

// HttpClient thực hiện yêu cầu HTTP. using giải phóng tài nguyên khi kết thúc;
// timeout giới hạn thời gian chờ, tránh treo khi máy chủ không phản hồi.
using var httpClient = new HttpClient
{
    BaseAddress = baseUri,
    Timeout = TimeSpan.FromSeconds(10)
};
Console.WriteLine($"Đang lấy danh sách món ăn từ {new Uri(baseUri, "/menu")}...");

try
{
    // GET /menu chỉ đọc danh sách, không cần body và không thay đổi dữ liệu API.
    using var response = await httpClient.GetAsync("/menu");

    // Kiểm tra mã HTTP trước khi đọc JSON: 404 là sai endpoint, 500 là lỗi máy chủ.
    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Không lấy được thực đơn. API trả về HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        return 1;
    }

    // Chuyển mảng JSON thành danh sách C#; mặc định hỗ trợ tên camelCase của API.
    var menu = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();
    if (menu is null)
    {
        // null khác mảng rỗng: hợp đồng API phải trả về một danh sách.
        Console.Error.WriteLine("Dữ liệu không hợp lệ: API trả về null thay vì danh sách món ăn.");
        return 1;
    }
    if (menu.Count == 0)
    {
        Console.WriteLine("Thực đơn hiện chưa có món ăn.");
        return 0;
    }

    // Định dạng giá theo Việt Nam, ví dụ 55.000 VND.
    // In từng món trên nhiều dòng để tên/mô tả dài không làm lệch bảng.
    var vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    Console.WriteLine($"\nDANH SÁCH MÓN ĂN ({menu.Count} món)");
    Console.WriteLine(new string('=', 60));
    foreach (var item in menu)
    {
        Console.WriteLine($"[{item.Id}] {item.Name}");
        Console.WriteLine($"  Nhóm món  : {item.Category}");
        Console.WriteLine($"  Mô tả     : {item.Description}");
        Console.WriteLine($"  Giá       : {item.Price.ToString("N0", vietnamese)} VND");
        Console.WriteLine($"  Trạng thái: {(item.IsAvailable ? "Có thể đặt" : "Tạm hết món")}");
        Console.WriteLine(new string('-', 60));
    }
    // Mã thoát 0 biểu thị thành công; 1 biểu thị lỗi để shell có thể nhận biết.
    return 0;
}
catch (HttpRequestException)
{
    // API chưa chạy, sai cổng/địa chỉ hoặc kết nối bị ngắt giữa chừng.
    Console.Error.WriteLine("Không kết nối được API hoặc kết nối bị gián đoạn. Hãy kiểm tra máy chủ và địa chỉ API.");
    return 1;
}
catch (TaskCanceledException)
{
    Console.Error.WriteLine("Yêu cầu quá thời gian chờ 10 giây. Vui lòng thử lại.");
    return 1;
}
catch (JsonException)
{
    // Ví dụ máy chủ trả HTML hoặc JSON không đúng cấu trúc mảng món ăn.
    Console.Error.WriteLine("Không đọc được thực đơn: dữ liệu API không đúng định dạng JSON mong đợi.");
    return 1;
}
