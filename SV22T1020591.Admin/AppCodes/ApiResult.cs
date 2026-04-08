namespace SV22T1020591.Admin
{
    /// <summary>
    /// Biểu diễn dữ liệu trả về của các API trong vùng Admin
    /// </summary>
    public class ApiResult
    {
        public ApiResult(int code, string message = "")
        {
            Code = code;
            Message = message;
        }

        public int Code { get; set; }
        public string Message { get; set; } = "";

        // Optional payload: redirect url, id, object, ...
        public object? Data { get; set; }
    }
}
