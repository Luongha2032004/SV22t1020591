using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Partner;
using System.Data;

namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Lớp cài đặt các chức năng thao tác dữ liệu cho bảng Shippers
    /// sử dụng thư viện Dapper để làm việc với SQL Server
    /// </summary>
    public class ShipperRepository : IGenericRepository<Shipper>
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor của lớp ShipperRepository
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến cơ sở dữ liệu SQL Server</param>
        public ShipperRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Tạo và mở kết nối đến cơ sở dữ liệu
        /// </summary>
        /// <returns>Đối tượng kết nối IDbConnection</returns>
        private IDbConnection OpenConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Truy vấn danh sách người giao hàng theo điều kiện tìm kiếm và phân trang
        /// </summary>
        /// <param name="input">Thông tin tìm kiếm và phân trang</param>
        /// <returns>Kết quả danh sách người giao hàng</returns>
        public async Task<PagedResult<Shipper>> ListAsync(PaginationSearchInput input)
        {
            using var connection = OpenConnection();

            var result = new PagedResult<Shipper>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string searchValue = $"%{input.SearchValue}%";

            string countSql = @"SELECT COUNT(*)
                                FROM Shippers
                                WHERE ShipperName LIKE @SearchValue";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql,
                new { SearchValue = searchValue });

            string dataSql = @"SELECT *
                               FROM Shippers
                               WHERE ShipperName LIKE @SearchValue
                               ORDER BY ShipperName
                               OFFSET @Offset ROWS
                               FETCH NEXT @PageSize ROWS ONLY";

            if (input.PageSize == 0)
            {
                dataSql = @"SELECT *
                            FROM Shippers
                            WHERE ShipperName LIKE @SearchValue
                            ORDER BY ShipperName";
            }

            var data = await connection.QueryAsync<Shipper>(dataSql,
                new
                {
                    SearchValue = searchValue,
                    Offset = input.Offset,
                    PageSize = input.PageSize
                });

            result.DataItems = data.ToList();

            return result;
        }

        /// <summary>
        /// Lấy thông tin một người giao hàng theo ShipperID
        /// </summary>
        /// <param name="id">Mã người giao hàng</param>
        /// <returns>Thông tin người giao hàng hoặc null nếu không tồn tại</returns>
        public async Task<Shipper?> GetAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"SELECT *
                           FROM Shippers
                           WHERE ShipperID = @ShipperID";

            return await connection.QueryFirstOrDefaultAsync<Shipper>(sql,
                new { ShipperID = id });
        }

        /// <summary>
        /// Thêm mới một người giao hàng vào CSDL
        /// </summary>
        /// <param name="data">Thông tin người giao hàng</param>
        /// <returns>Mã ShipperID của bản ghi vừa được thêm</returns>
        public async Task<int> AddAsync(Shipper data)
        {
            using var connection = OpenConnection();

            string sql = @"INSERT INTO Shippers
                           (ShipperName, Phone)
                           VALUES
                           (@ShipperName, @Phone);
                           SELECT SCOPE_IDENTITY();";

            int id = await connection.ExecuteScalarAsync<int>(sql, data);

            return id;
        }

        /// <summary>
        /// Cập nhật thông tin người giao hàng
        /// </summary>
        /// <param name="data">Dữ liệu cần cập nhật</param>
        /// <returns>True nếu cập nhật thành công</returns>
        public async Task<bool> UpdateAsync(Shipper data)
        {
            using var connection = OpenConnection();

            string sql = @"UPDATE Shippers
                           SET ShipperName = @ShipperName,
                               Phone = @Phone
                           WHERE ShipperID = @ShipperID";

            int rows = await connection.ExecuteAsync(sql, data);

            return rows > 0;
        }

        /// <summary>
        /// Xóa một người giao hàng theo ShipperID
        /// </summary>
        /// <param name="id">Mã người giao hàng</param>
        /// <returns>True nếu xóa thành công</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"DELETE FROM Shippers
                           WHERE ShipperID = @ShipperID";

            int rows = await connection.ExecuteAsync(sql,
                new { ShipperID = id });

            return rows > 0;
        }

        /// <summary>
        /// Kiểm tra người giao hàng có đang được sử dụng trong bảng Orders hay không
        /// </summary>
        /// <param name="id">Mã người giao hàng</param>
        /// <returns>True nếu đã được sử dụng</returns>
        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"SELECT COUNT(*)
                           FROM Orders
                           WHERE ShipperID = @ShipperID";

            int count = await connection.ExecuteScalarAsync<int>(sql,
                new { ShipperID = id });

            return count > 0;
        }
    }
}