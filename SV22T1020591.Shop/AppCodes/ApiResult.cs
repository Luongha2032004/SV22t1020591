namespace SV22T1020591.Shop
{
    /// <summary>
    /// Biểu diễn dữ liệu trả về của các API 
    /// </summary>
    public class ApiResult
    {
        /// <summary>
        /// Trả về kết quả của một API (mặc định là lỗi)
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        public ApiResult(int code, string message = "") 
        { 
            Code = code;
            Message = message;
        }
        /// <summary>
        /// Mã kết quả trả về ( quy ước 0 tức là lỗi hoặc không thành công)
        /// </summary>
        public int Code { get; set; }
        /// <summary>
        /// Thông báo lỗi (nếu có)
        /// </summary>
        public string Message { get; set; } = "";
    }
}
