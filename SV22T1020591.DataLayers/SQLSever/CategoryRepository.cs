using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Catalog;
using SV22T1020591.Models.Common;
namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Lớp thực hiện các thao tác truy xuất dữ liệu bảng Categories trong SQL Server
    /// thông qua thư viện Dapper.
    /// </summary>
    public class CategoryRepository : IGenericRepository<Category>
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor khởi tạo repository với chuỗi kết nối CSDL
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến SQL Server</param>
        public CategoryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Bổ sung một loại hàng mới vào CSDL
        /// </summary>
        /// <param name="data">Dữ liệu loại hàng cần thêm</param>
        /// <returns>Mã CategoryID của bản ghi vừa được thêm</returns>
        public async Task<int> AddAsync(Category data)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"INSERT INTO Categories(CategoryName, Description)
                           VALUES(@CategoryName, @Description);
                           SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        /// <summary>
        /// Xóa loại hàng theo CategoryID
        /// </summary>
        /// <param name="id">Mã loại hàng cần xóa</param>
        /// <returns>True nếu xóa thành công, ngược lại False</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = "DELETE FROM Categories WHERE CategoryID = @id";

            int rows = await connection.ExecuteAsync(sql, new { id });
            return rows > 0;
        }

        /// <summary>
        /// Lấy thông tin loại hàng theo CategoryID
        /// </summary>
        /// <param name="id">Mã loại hàng</param>
        /// <returns>Đối tượng Category nếu tồn tại, ngược lại null</returns>
        public async Task<Category?> GetAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = "SELECT * FROM Categories WHERE CategoryID = @id";

            return await connection.QueryFirstOrDefaultAsync<Category>(sql, new { id });
        }

        /// <summary>
        /// Kiểm tra loại hàng có đang được sử dụng trong bảng Products hay không
        /// </summary>
        /// <param name="id">Mã loại hàng</param>
        /// <returns>True nếu có dữ liệu liên quan, ngược lại False</returns>
        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"SELECT COUNT(*)
                           FROM Products
                           WHERE CategoryID = @id";

            int count = await connection.ExecuteScalarAsync<int>(sql, new { id });
            return count > 0;
        }

        /// <summary>
        /// Truy vấn danh sách loại hàng có phân trang và tìm kiếm
        /// </summary>
        /// <param name="input">Thông tin tìm kiếm và phân trang</param>
        /// <returns>Kết quả danh sách loại hàng được phân trang</returns>
        public async Task<PagedResult<Category>> ListAsync(PaginationSearchInput input)
        {
            using var connection = new SqlConnection(_connectionString);

            var result = new PagedResult<Category>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string countSql = @"SELECT COUNT(*)
                        FROM Categories
                        WHERE CategoryName LIKE @SearchValue";

            result.RowCount = await connection.ExecuteScalarAsync<int>(
                countSql,
                new { SearchValue = $"%{input.SearchValue}%" }
            );

            List<Category> data;

            // 🔥 QUAN TRỌNG: xử lý riêng 2 trường hợp
            if (input.PageSize > 0)
            {
                string dataSql = @"SELECT *
                           FROM Categories
                           WHERE CategoryName LIKE @SearchValue
                           ORDER BY CategoryName
                           OFFSET @Offset ROWS
                           FETCH NEXT @PageSize ROWS ONLY";

                data = (await connection.QueryAsync<Category>(
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
                // 👉 KHÔNG dùng FETCH khi PageSize = 0
                string dataSql = @"SELECT *
                           FROM Categories
                           WHERE CategoryName LIKE @SearchValue
                           ORDER BY CategoryName";

                data = (await connection.QueryAsync<Category>(
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
        /// Cập nhật thông tin loại hàng
        /// </summary>
        /// <param name="data">Dữ liệu loại hàng cần cập nhật</param>
        /// <returns>True nếu cập nhật thành công, ngược lại False</returns>
        /// 
        public async Task<bool> UpdateAsync(Category data)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"UPDATE Categories
                           SET CategoryName = @CategoryName,
                               Description = @Description
                           WHERE CategoryID = @CategoryID";

            int rows = await connection.ExecuteAsync(sql, data);
            return rows > 0;
        }
    }
}