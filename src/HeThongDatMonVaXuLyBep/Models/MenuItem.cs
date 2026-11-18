namespace HeThongDatMonVaXuLyBep.Models;

/// <summary>
/// Mô tả một món ăn hoặc đồ uống trong thực đơn trả về từ GET /menu.
/// Dùng record để biểu diễn dữ liệu với các thuộc tính chỉ khởi tạo khi tạo đối tượng.
/// </summary>
/// <param name="Id">Mã định danh duy nhất của món trong danh sách mẫu.</param>
/// <param name="Name">Tên món hiển thị cho khách hàng.</param>
/// <param name="Description">Mô tả thành phần hoặc khẩu phần của món.</param>
/// <param name="Category">Nhóm món, ví dụ Món chính, Món khai vị hoặc Đồ uống.</param>
/// <param name="Price">
/// Giá bán một phần, đơn vị VND. Dùng decimal để biểu diễn tiền theo hệ thập phân,
/// tránh sai số biểu diễn thường gặp khi dùng float hoặc double.
/// </param>
/// <param name="IsAvailable">True nếu món đang có thể đặt; false nếu tạm hết món.</param>
public record MenuItem(
    int Id,
    string Name,
    string Description,
    string Category,
    decimal Price,
    bool IsAvailable);
