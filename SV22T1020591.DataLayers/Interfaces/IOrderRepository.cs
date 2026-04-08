using SV22T1020591.Models.Common;
using SV22T1020591.Models.Sales;

namespace SV22T1020591.DataLayers.Interfaces
{
    /// <summary>
    /// Định nghĩa các chức năng xử lý dữ liệu cho đơn hàng
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// Tìm kiếm và lấy danh sách đơn hàng dưới dạng phân trang
        /// </summary>
        Task<PagedResult<OrderSearchInfo>> ListAsync(OrderSearchInput input);

        /// <summary>
        /// Lấy danh sách đơn hàng của khách hàng
        /// </summary>
        Task<PagedResult<OrderSearchInfo>> ListByCustomerAsync(int customerID, OrderSearchInput input);

        /// <summary>
        /// Lấy thông tin 1 đơn hàng
        /// </summary>
        Task<OrderViewInfo?> GetAsync(int orderID);

        /// <summary>
        /// Lấy thông tin 1 đơn hàng của khách hàng
        /// </summary>
        Task<OrderViewInfo?> GetByCustomerAsync(int customerID, int orderID);

        /// <summary>
        /// Bổ sung đơn hàng
        /// </summary>
        Task<int> AddAsync(Order data);

        /// <summary>
        /// Cập nhật đơn hàng
        /// </summary>
        Task<bool> UpdateAsync(Order data);

        /// <summary>
        /// Xóa đơn hàng
        /// </summary>
        Task<bool> DeleteAsync(int orderID);

        /// <summary>
        /// Lấy danh sách chi tiết đơn hàng
        /// </summary>
        Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID);

        /// <summary>
        /// Lấy chi tiết 1 mặt hàng trong đơn
        /// </summary>
        Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID);

        /// <summary>
        /// Thêm mặt hàng vào đơn
        /// </summary>
        Task<bool> AddDetailAsync(OrderDetail data);

        /// <summary>
        /// Cập nhật mặt hàng trong đơn
        /// </summary>
        Task<bool> UpdateDetailAsync(OrderDetail data);

        /// <summary>
        /// Xóa mặt hàng khỏi đơn
        /// </summary>
        Task<bool> DeleteDetailAsync(int orderID, int productID);
    }
}
