using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Security;


namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Xử lý dữ liệu tài khoản đăng nhập của khách hàng
    /// </summary>
    public class CustomerAccountRepository : IUserAccountRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString"></param>
        public CustomerAccountRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Kiểm tra đăng nhập của khách hàng
        /// </summary>
        /// <param name="userName">Email</param>
        /// <param name="password">Mật khẩu</param>
        /// <returns>Thông tin tài khoản nếu hợp lệ, ngược lại null</returns>
        public async Task<UserAccount?> AuthenticateAsync(string userName, string password)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"SELECT CustomerID AS UserId,
                                  Email AS UserName,
                                  CustomerName AS DisplayName,
                                  'Customer' AS RoleNames
                           FROM Customers
                           WHERE Email = @userName 
                                 AND Password = @password
                                 AND (IsLocked = 0 OR IsLocked IS NULL)";

            return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, new
            {
                userName,
                password
            });
        }

        /// <summary>
        /// Đổi mật khẩu của khách hàng
        /// </summary>
        /// <param name="userName">Email</param>
        /// <param name="password">Mật khẩu mới</param>
        /// <returns>True nếu đổi thành công</returns>
        public async Task<bool> ChangePassword(string userName, string password)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"UPDATE Customers
                           SET Password = @password
                           WHERE Email = @userName";

            int rows = await connection.ExecuteAsync(sql, new
            {
                userName,
                password
            });

            return rows > 0;
        }
    }
}

