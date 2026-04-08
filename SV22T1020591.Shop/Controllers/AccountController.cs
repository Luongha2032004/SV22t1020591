using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SV22T1020591.Models.Partner;
using SV22T1020591.BusinessLayers;

namespace SV22T1020591.Shop.Controllers
{
    /// <summary>
    /// Controller xử lý các chức năng liên quan đến tài khoản người dùng:
    /// đăng nhập, đăng xuất, đăng ký, cập nhật thông tin cá nhân và đổi mật khẩu.
    /// </summary>
    public class AccountController : Controller
    {
        // ================= LOGIN =================

        /// <summary>
        /// Hiển thị trang đăng nhập.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Xử lý đăng nhập.
        /// Kiểm tra thông tin, xác thực tài khoản và tạo session + cookie đăng nhập.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                ViewBag.Username = username;

                if (string.IsNullOrWhiteSpace(username))
                    ModelState.AddModelError("username", "Vui lòng nhập email");

                if (string.IsNullOrWhiteSpace(password))
                    ModelState.AddModelError("password", "Vui lòng nhập mật khẩu");

                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", "⚠ Vui lòng nhập đầy đủ thông tin");
                    return View();
                }

                string hashed = CryptHelper.HashMD5(password);

                var repo = new SV22T1020591.DataLayers.SQLServer.CustomerAccountRepository(
                    ApplicationContext.Configuration?.GetConnectionString("LiteCommerceDB")
                    ?? SV22T1020591.BusinessLayers.Configuration.ConnectionString
                );

                var user = await repo.AuthenticateAsync(username, hashed);

                if (user == null)
                {
                    ModelState.AddModelError("", "❌ Đăng nhập thất bại");
                    return View();
                }

                var customer = await PartnerDataService.GetCustomerAsync(int.Parse(user.UserId));
                ApplicationContext.SetSessionData("Customer", customer);

                var userData = new WebUserData()
                {
                    UserId = user.UserId.ToString(),
                    UserName = user.UserName,
                    DisplayName = user.DisplayName,
                    Email = user.UserName,
                    Roles = user.RoleNames?.Split(',', ';').ToList() ?? new List<string>()
                };

                var principal = userData.CreatePrincipal();
                await HttpContext.SignInAsync("CustomerCookie", principal);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ Lỗi hệ thống: " + ex.Message);
                return View();
            }
        }

        // ================= LOGOUT =================

        /// <summary>
        /// Đăng xuất người dùng khỏi hệ thống.
        /// Xóa cookie xác thực.
        /// </summary>
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync("CustomerCookie");
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        // ================= PROVINCE =================

        /// <summary>
        /// Lấy danh sách tỉnh/thành để hiển thị dropdown.
        /// </summary>
        private async Task<List<SelectListItem>> GetProvinceSelectListAsync(string? selectedValue = null)
        {
            try
            {
                var list = new List<SelectListItem>()
                {
                    new SelectListItem() { Value = "", Text = "-- Tỉnh/Thành phố --" }
                };

                var provinces = await DictionaryDataService.ListProvincesAsync();
                foreach (var p in provinces)
                {
                    list.Add(new SelectListItem()
                    {
                        Value = p.ProvinceName,
                        Text = p.ProvinceName,
                        Selected = string.Equals(p.ProvinceName, selectedValue, StringComparison.OrdinalIgnoreCase)
                    });
                }

                return list;
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }

        // ================= PROFILE =================

        /// <summary>
        /// Hiển thị thông tin cá nhân của người dùng đang đăng nhập.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var user = User.GetUserData();
                if (user == null) return RedirectToAction("Login");

                int id = int.Parse(user.UserId);
                var customer = await PartnerDataService.GetCustomerAsync(id);

                ViewBag.Provinces = await GetProvinceSelectListAsync(customer?.Province);

                return View(customer);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân của người dùng.
        /// Bao gồm validate dữ liệu trước khi lưu.
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Profile(Customer model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.CustomerName))
                    ModelState.AddModelError(nameof(model.CustomerName), "Vui lòng nhập họ tên");

                if (string.IsNullOrWhiteSpace(model.Phone))
                    ModelState.AddModelError(nameof(model.Phone), "Vui lòng nhập số điện thoại");

                if (string.IsNullOrWhiteSpace(model.Address))
                    ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ");

                if (string.IsNullOrWhiteSpace(model.ContactName))
                    ModelState.AddModelError(nameof(model.ContactName), "Vui lòng nhập tên giao dịch");

                if (string.IsNullOrWhiteSpace(model.Email))
                    ModelState.AddModelError(nameof(model.Email), "Vui lòng nhập email");

                if (string.IsNullOrWhiteSpace(model.Province))
                    ModelState.AddModelError(nameof(model.Province), "Vui lòng chọn tỉnh/thành");
                else
                    model.Province = model.Province.Trim();

                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", "⚠ Vui lòng nhập đầy đủ thông tin");
                    ViewBag.Provinces = await GetProvinceSelectListAsync(model?.Province);
                    return View(model);
                }

                model.ContactName ??= model.CustomerName;
                model.Phone ??= "";
                model.Address ??= "";

                await PartnerDataService.UpdateCustomerAsync(model);

                ViewBag.Message = "✅ Cập nhật thành công";
                ViewBag.Provinces = await GetProvinceSelectListAsync(model.Province);

                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ Lỗi: " + ex.Message);
                ViewBag.Provinces = await GetProvinceSelectListAsync(model?.Province);
                return View(model);
            }
        }

        // ================= CHANGE PASSWORD =================

        /// <summary>
        /// Hiển thị trang đổi mật khẩu.
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Xử lý đổi mật khẩu.
        /// Kiểm tra mật khẩu cũ và cập nhật mật khẩu mới.
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            try
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

                var repo = new SV22T1020591.DataLayers.SQLServer.CustomerAccountRepository(
                    ApplicationContext.Configuration?.GetConnectionString("LiteCommerceDB")
                    ?? SV22T1020591.BusinessLayers.Configuration.ConnectionString);

                string hashedOld = CryptHelper.HashMD5(oldPassword);
                var exist = await repo.AuthenticateAsync(user.Email, hashedOld);

                if (exist == null)
                {
                    ModelState.AddModelError("", "❌ Mật khẩu cũ không đúng");
                    return View();
                }

                string hashedNew = CryptHelper.HashMD5(newPassword);
                var ok = await repo.ChangePassword(user.Email, hashedNew);

                if (!ok)
                {
                    ModelState.AddModelError("", "❌ Không thể đổi mật khẩu");
                    return View();
                }

                ViewBag.Message = "✅ Đổi mật khẩu thành công";
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ Lỗi: " + ex.Message);
                return View();
            }
        }

        // ================= REGISTER =================

        /// <summary>
        /// Hiển thị trang đăng ký tài khoản.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Xử lý đăng ký tài khoản mới.
        /// Kiểm tra dữ liệu và lưu khách hàng vào hệ thống.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string customerName, string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customerName))
                    ModelState.AddModelError("customerName", "Vui lòng nhập họ tên");

                if (string.IsNullOrWhiteSpace(email))
                    ModelState.AddModelError("email", "Vui lòng nhập email");

                if (string.IsNullOrWhiteSpace(password))
                    ModelState.AddModelError("password", "Vui lòng nhập mật khẩu");

                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", "⚠ Vui lòng nhập đầy đủ thông tin");
                    return View();
                }

                bool isValid = await PartnerDataService.ValidatelCustomerEmailAsync(email);
                if (!isValid)
                {
                    ModelState.AddModelError("email", "Email đã tồn tại");
                    return View();
                }

                string hashed = CryptHelper.HashMD5(password);

                var customer = new Customer()
                {
                    CustomerName = customerName,
                    ContactName = customerName,
                    Email = email,
                    Password = hashed
                };

                await PartnerDataService.AddCustomerAsync(customer);

                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ Lỗi: " + ex.Message);
                return View();
            }
        }

        /// <summary>
        /// Trang hiển thị khi người dùng không có quyền truy cập.
        /// </summary>
        public IActionResult AccessDenied()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }
    }
}