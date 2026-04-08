using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Partner;

namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Lớp thực hiện các thao tác truy xuất dữ liệu bảng Supplier trong SQL Server
    /// thông qua thư viện Dapper.
    /// </summary>
    public class SupplierRepository : IGenericRepository<Supplier>
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor khởi tạo repository với chuỗi kết nối CSDL
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến SQL Server</param>
        public SupplierRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Bổ sung một nhà cung cấp mới vào CSDL
        /// </summary>
        /// <param name="data">Dữ liệu nhà cung cấp cần thêm</param>
        /// <returns>Mã SupplierID của bản ghi vừa được thêm</returns>
        public async Task<int> AddAsync(Supplier data)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"INSERT INTO Suppliers
                           (SupplierName, ContactName, Province, Address, Phone, Email)
                           VALUES
                           (@SupplierName, @ContactName, @Province, @Address, @Phone, @Email);
                           SELECT CAST(SCOPE_IDENTITY() as int);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        /// <summary>
        /// Xóa nhà cung cấp theo SupplierID
        /// </summary>
        /// <param name="id">Mã nhà cung cấp cần xóa</param>
        /// <returns>True nếu xóa thành công, ngược lại False</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = "DELETE FROM Suppliers WHERE SupplierID = @id";

            int rows = await connection.ExecuteAsync(sql, new { id });
            return rows > 0;
        }

        /// <summary>
        /// Lấy thông tin nhà cung cấp theo SupplierID
        /// </summary>
        /// <param name="id">Mã nhà cung cấp cần lấy</param>
        /// <returns>Đối tượng Supplier nếu tồn tại, ngược lại null</returns>
        public async Task<Supplier?> GetAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = "SELECT * FROM Suppliers WHERE SupplierID = @id";

            return await connection.QueryFirstOrDefaultAsync<Supplier>(sql, new { id });
        }

        /// <summary>
        /// Kiểm tra nhà cung cấp có đang được sử dụng bởi dữ liệu khác hay không
        /// </summary>
        /// <param name="id">Mã nhà cung cấp</param>
        /// /// <returns>True nếu có dữ liệu liên quan, ngược lại False</returns>
        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"SELECT COUNT(*) 
                           FROM Products 
                           WHERE SupplierID = @id";

            int count = await connection.ExecuteScalarAsync<int>(sql, new { id });
            return count > 0;
        }

        /// <summary>
        /// Truy vấn danh sách nhà cung cấp có phân trang và tìm kiếm
        /// </summary>
        /// <param name="input">Thông tin tìm kiếm và phân trang</param>
        /// <returns>Kết quả danh sách nhà cung cấp được phân trang</returns>
        public async Task<PagedResult<Supplier>> ListAsync(PaginationSearchInput input)
        {
            using var connection = new SqlConnection(_connectionString);

            var result = new PagedResult<Supplier>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string countSql = @"SELECT COUNT(*) 
                        FROM Suppliers
                        WHERE SupplierName LIKE @SearchValue 
                           OR ContactName LIKE @SearchValue";

            result.RowCount = await connection.ExecuteScalarAsync<int>(
                countSql,
                new { SearchValue = $"%{input.SearchValue}%" }
            );

            List<Supplier> data;

            // 🔥 FIX GIỐNG CATEGORY
            if (input.PageSize > 0)
            {
                string dataSql = @"SELECT *
                           FROM Suppliers
                           WHERE SupplierName LIKE @SearchValue
                              OR ContactName LIKE @SearchValue
                           ORDER BY SupplierName
                           OFFSET @Offset ROWS
                           FETCH NEXT @PageSize ROWS ONLY";

                data = (await connection.QueryAsync<Supplier>(
                    dataSql,
                    new
                    {
                        SearchValue = $"%{input.SearchValue}%",
                        Offset = input.Offset,
                        PageSize = input.PageSize
                    })).ToList();
            }
            else
            {
                // ❌ KHÔNG dùng FETCH khi PageSize = 0
                string dataSql = @"SELECT *
                           FROM Suppliers
                           WHERE SupplierName LIKE @SearchValue
                              OR ContactName LIKE @SearchValue
                           ORDER BY SupplierName";

                data = (await connection.QueryAsync<Supplier>(
                    dataSql,
                    new
                    {
                        SearchValue = $"%{input.SearchValue}%"
                    })).ToList();
            }

            result.DataItems = data;
            return result;
        }

        /// <summary>
        /// Cập nhật thông tin nhà cung cấp
        /// </summary>
        /// <param name="data">Dữ liệu nhà cung cấp cần cập nhật</param>
        /// <returns>True nếu cập nhật thành công, ngược lại False</returns>
        public async Task<bool> UpdateAsync(Supplier data)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"UPDATE Suppliers
                           SET SupplierName = @SupplierName,
                               ContactName = @ContactName,
                               Province = @Province,
                               Address = @Address,
                               Phone = @Phone,
                               Email = @Email
                           WHERE SupplierID = @SupplierID";

            int rows = await connection.ExecuteAsync(sql, data);
            return rows > 0;
        }
    }
}