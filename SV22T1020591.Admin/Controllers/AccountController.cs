using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020591.Admin;
using SV22T1020591.Models.Security;
using SV22T1020591.DataLayers.SQLServer;

namespace SV22T1020591.Admin.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến tài khoản
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        /// <summary>
        /// Đăng nhập vào hệ thống
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous] //cho phép truy cập vào chức năng này mà không cần đăng nhập
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            ViewBag.Username = username;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("Error", "Vui lòng nhập tên đăng nhập và mật khẩu");
                return View();
            }

            try
            {
                string hashedPassword = CryptHelper.HashMD5(password);

                var repo = new EmployeeAccountRepository(
                    ApplicationContext.Configuration?.GetConnectionString("LiteCommerceDB")
                    ?? SV22T1020591.BusinessLayers.Configuration.ConnectionString
                );

                // Authenticate against database
                var userAccount = await repo.AuthenticateAsync(username, hashedPassword);

                if (userAccount == null)
                {
                    ModelState.AddModelError("Error", "Đăng nhập thất bại - kiểm tra tên hoặc mật khẩu");
                    return View();
                }

                // Chuẩn bị thông tin ghi lên "giấy chứng nhận"
                var userData = new WebUserData()
                {
                    UserId = userAccount.UserId,
                    UserName = userAccount.UserName,
                    DisplayName = userAccount.DisplayName,
                    Email = userAccount.Email,
                    Photo = string.IsNullOrEmpty(userAccount.Photo) ? "nophoto.png" : userAccount.Photo,
                    Roles = (userAccount.RoleNames ?? "")
                                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(r => r.Trim()).ToList()
                };

                // Tạo ClaimsPrincipal và sign-in
                var principal = userData.CreatePrincipal();
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View();
            }
        }

        /// <summary>
        /// Đăng xuất khỏi hệ thống
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        /// <summary>
        /// Thay đổi mật khẩu của người dùng
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        /// <summary>
        /// Xử lý thay đổi mật khẩu (người dùng tự đổi)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            var user = User.GetUserData();
            if (user == null)
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(oldPassword))
                ModelState.AddModelError("oldPassword", "Vui lòng nhập mật khẩu cũ");

            if (string.IsNullOrWhiteSpace(newPassword))
                ModelState.AddModelError("newPassword", "Vui lòng nhập mật khẩu mới");

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "⚠ Vui lòng nhập đầy đủ thông tin");
                return View();
            }

            try
            {
                var repo = new EmployeeAccountRepository(
                    ApplicationContext.Configuration?.GetConnectionString("LiteCommerceDB")
                    ?? SV22T1020591.BusinessLayers.Configuration.ConnectionString
                );

                string hashedOld = CryptHelper.HashMD5(oldPassword);
                var exist = await repo.AuthenticateAsync(user.UserName, hashedOld); // Sửa thành user.UserName

                if (exist == null)
                {
                    ModelState.AddModelError("", "Mật khẩu cũ không đúng");
                    return View();
                }

                string hashedNew = CryptHelper.HashMD5(newPassword);
                var ok = await repo.ChangePassword(user.UserName, hashedNew); // Sửa thành user.UserName

                if (!ok)
                {
                    ModelState.AddModelError("", "Không thể đổi mật khẩu");
                    return View();
                }

                ViewBag.Message = "Đổi mật khẩu thành công";
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View();
            }
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}