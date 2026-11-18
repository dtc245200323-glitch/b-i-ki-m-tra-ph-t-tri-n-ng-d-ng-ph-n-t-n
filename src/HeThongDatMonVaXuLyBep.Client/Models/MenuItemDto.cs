namespace HeThongDatMonVaXuLyBep.Client.Models;

/// <summary>
/// DTO (Data Transfer Object) nhận một món ăn từ JSON của GET /menu.
/// Client có kiểu dữ liệu riêng nên không cần tham chiếu project Web API.
/// Các trường cần khớp với hợp đồng dữ liệu của API.
/// </summary>
/// <param name="Id">Mã món ăn.</param>
/// <param name="Name">Tên món ăn hoặc đồ uống.</param>
/// <param name="Description">Mô tả thành phần hoặc khẩu phần.</param>
/// <param name="Category">Nhóm món.</param>
/// <param name="Price">Giá bán một phần, đơn vị VND, sử dụng decimal.</param>
/// <param name="IsAvailable">Cho biết món có đang được phép đặt hay không.</param>
public record MenuItemDto(
    int Id,
    string Name,
    string Description,
    string Category,
    decimal Price,
    bool IsAvailable);
