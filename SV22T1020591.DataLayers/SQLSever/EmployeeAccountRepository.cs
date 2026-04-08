using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Security;

namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Xử lý dữ liệu tài khoản đăng nhập của nhân viên
    /// </summary>
    public class EmployeeAccountRepository : IUserAccountRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString"></param>
        public EmployeeAccountRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Kiểm tra đăng nhập của nhân viên
        /// </summary>
        /// <param name="userName">Email</param>
        /// <param name="password">Mật khẩu</param>
        /// <returns>Thông tin tài khoản nếu hợp lệ, ngược lại null</returns>
        public async Task<UserAccount?> AuthenticateAsync(string userName, string password)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"SELECT EmployeeID AS UserId,
                      Email AS UserName,
                      FullName AS DisplayName,
                      Email AS Email,
                      Photo AS Photo,
                      RoleNames
               FROM Employees
               WHERE Email = @userName
                     AND Password = @password
                     AND IsWorking = 1";


            return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, new
            {
                userName,
                password
            });
        }

        /// <summary>
        /// Đổi mật khẩu của nhân viên
        /// </summary>
        /// <param name="userName">Email</param>
        /// <param name="password">Mật khẩu mới</param>
        /// <returns>True nếu đổi thành công</returns>
        public async Task<bool> ChangePassword(string userName, string password)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"
        UPDATE Employees
        SET Password = @password
        WHERE Email = @userName
              AND IsWorking = 1";

            int rows = await connection.ExecuteAsync(sql, new
            {
                userName,
                password
            });

            return rows > 0;
        }

    }
}