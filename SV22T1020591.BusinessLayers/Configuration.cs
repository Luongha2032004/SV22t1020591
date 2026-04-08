using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV22T1020591.BusinessLayers
{
    /// <summary>
    /// lớp lưu trữ các hằng số cấu hình của ứng dụng, có thể được sử dụng trong toàn bộ ứng dụng
    /// </summary>
    public static class Configuration
    {
        private static string _connectionString = "";
        /// <summary>
        /// hàm có chức năng khởi các cấu hình cho buniessslayer 
        /// (hàm này phải được gọi trước khi khởi chạy ứng dụng 
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static void Initialize(String connectionString)
        {
            _connectionString = connectionString;
        }
        /// <summary>
        /// lấy chuỗi tham số kết nối đến cơ sở dữ liệu 
        /// (Configuration.ConnectionString)
        /// </summary>
        public static string ConnectionString => _connectionString;

    }
}
