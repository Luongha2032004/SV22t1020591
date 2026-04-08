using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.HR;
using System.Data;

namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Lớp cài đặt các chức năng thao tác dữ liệu cho bảng Employees
    /// sử dụng thư viện Dapper để làm việc với SQL Server
    /// </summary>
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor của lớp EmployeeRepository
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến SQL Server</param>
        public EmployeeRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Mở kết nối đến cơ sở dữ liệu
        /// </summary>
        private IDbConnection OpenConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Truy vấn danh sách nhân viên theo điều kiện tìm kiếm và phân trang
        /// </summary>
        public async Task<PagedResult<Employee>> ListAsync(PaginationSearchInput input)
        {
            using var connection = OpenConnection();

            var result = new PagedResult<Employee>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string searchValue = $"%{input.SearchValue}%";

            string countSql = @"SELECT COUNT(*)
                                FROM Employees
                                WHERE FullName LIKE @SearchValue";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql,
                new { SearchValue = searchValue });

            string dataSql = @"SELECT *
                               FROM Employees
                               WHERE FullName LIKE @SearchValue
                               ORDER BY FullName
                               OFFSET @Offset ROWS
                               FETCH NEXT @PageSize ROWS ONLY";

            if (input.PageSize == 0)
            {
                dataSql = @"SELECT *
                            FROM Employees
                            WHERE FullName LIKE @SearchValue
                            ORDER BY FullName";
            }

            var data = await connection.QueryAsync<Employee>(dataSql,
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
        /// Lấy thông tin nhân viên theo EmployeeID
        /// </summary>
        public async Task<Employee?> GetAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"SELECT *
                           FROM Employees
                           WHERE EmployeeID = @EmployeeID";

            return await connection.QueryFirstOrDefaultAsync<Employee>(sql,
                new { EmployeeID = id });
        }

        /// <summary>
        /// Thêm mới nhân viên vào cơ sở dữ liệu
        /// </summary>
        public async Task<int> AddAsync(Employee data)
        {
            using var connection = OpenConnection();

            string sql = @"INSERT INTO Employees
                           (FullName, BirthDate, Address, Phone, Email, Photo, IsWorking)
                           VALUES
                           (@FullName, @BirthDate, @Address, @Phone, @Email, @Photo, @IsWorking);
                           SELECT SCOPE_IDENTITY();";

            int id = await connection.ExecuteScalarAsync<int>(sql, data);

            return id;
        }

        /// <summary>
        /// Cập nhật thông tin nhân viên
        /// </summary>
        public async Task<bool> UpdateAsync(Employee data)
        {
            using var connection = OpenConnection();

            string sql = @"UPDATE Employees
                           SET FullName = @FullName,
                               BirthDate = @BirthDate,
                               Address = @Address,
                               Phone = @Phone,
                               Email = @Email,
                               Photo = @Photo,
                               IsWorking = @IsWorking
                           WHERE EmployeeID = @EmployeeID";

            int rows = await connection.ExecuteAsync(sql, data);

            return rows > 0;
        }

        /// <summary>
        /// Xóa nhân viên theo EmployeeID
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"DELETE FROM Employees
                           WHERE EmployeeID = @EmployeeID";

            int rows = await connection.ExecuteAsync(sql,
                new { EmployeeID = id });

            return rows > 0;
        }

        /// <summary>
        /// Kiểm tra nhân viên có đang được sử dụng trong bảng Orders hay không
        /// </summary>
        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = OpenConnection();

            string sql = @"SELECT COUNT(*)
                           FROM Orders
                           WHERE EmployeeID = @EmployeeID";

            int count = await connection.ExecuteScalarAsync<int>(sql,
                new { EmployeeID = id });

            return count > 0;
        }

        /// <summary>
        /// Kiểm tra email của nhân viên có hợp lệ (không trùng) hay không
        /// </summary>
        public async Task<bool> ValidateEmailAsync(string email, int id = 0)
        {
            using var connection = OpenConnection();

            string sql;

            if (id == 0)
            {
                sql = @"SELECT COUNT(*)
                        FROM Employees
                        WHERE Email = @Email";

                int count = await connection.ExecuteScalarAsync<int>(sql,
                    new { Email = email });

                return count == 0;
            }
            else
            {
                sql = @"SELECT COUNT(*)
                        FROM Employees
                        WHERE Email = @Email
                        AND EmployeeID <> @EmployeeID";

                int count = await connection.ExecuteScalarAsync<int>(sql,
                    new { Email = email, EmployeeID = id });

                return count == 0;
            }
        }
    }
}