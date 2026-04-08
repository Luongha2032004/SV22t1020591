using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SV22T1020591.Admin;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.HR;
using SV22T1020591.DataLayers.SQLServer;

namespace SV22T1020591.Admin.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        /// <summary>
        /// Hiển thị danh sách nhân viên
        /// </summary>
        /// <returns>Trả về View danh sách nhân viên</returns>
        private const string EMPLOYEE_SEARCH = "EmployeeSearchInput";
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(EMPLOYEE_SEARCH);
            if (input == null)
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };
            return View(input);
        }
        /// <summary>
        /// Tìm kiếm trả về kết quả
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {

            var result = await HRDataService.ListEmployeesAsync(input);
            ApplicationContext.SetSessionData(EMPLOYEE_SEARCH, input);
            return View(result);
        }
        /// <summary>
        /// Hiển thị trang tạo mới nhân viên
        /// </summary>
        /// <returns>Trả về View nhập thông tin nhân viên</returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung nhân viên";
            var model = new Employee()
            {
                EmployeeID = 0,
                IsWorking = true
            };
            return View("Edit", model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin nhân viên";
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(Employee data, IFormFile? uploadPhoto)
        {
            try
            {
                ViewBag.Title = data.EmployeeID == 0 ? "Bổ sung nhân viên" : "Cập nhật thông tin nhân viên";

                //Kiểm tra dữ liệu đầu vào: FullName và Email là bắt buộc, Email chưa được sử dụng bởi nhân viên khác
                if (string.IsNullOrWhiteSpace(data.FullName))
                    ModelState.AddModelError(nameof(data.FullName), "Vui lòng nhập họ tên nhân viên");

                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng nhập email nhân viên");
                else if (!await HRDataService.ValidateEmployeeEmailAsync(data.Email, data.EmployeeID))
                    ModelState.AddModelError(nameof(data.Email), "Email đã được sử dụng bởi nhân viên khác");

                if (!ModelState.IsValid)
                    return View("Edit", data);

                //Xử lý upload ảnh
                if (uploadPhoto != null)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    var filePath = Path.Combine(ApplicationContext.WWWRootPath, "images/employees", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }
                    data.Photo = fileName;
                }

                //Tiền xử lý dữ liệu trước khi lưu vào database
                if (string.IsNullOrEmpty(data.Address)) data.Address = "";
                if (string.IsNullOrEmpty(data.Phone)) data.Phone = "";
                if (string.IsNullOrEmpty(data.Photo)) data.Photo = "nophoto.png";

                //Lưu dữ liệu vào database (bổ sung hoặc cập nhật)
                if (data.EmployeeID == 0)
                {
                    await HRDataService.AddEmployeeAsync(data);
                }
                else
                {
                    await HRDataService.UpdateEmployeeAsync(data);
                }
                return RedirectToAction("Index");
            }
            catch //(Exception ex)
            {
                //TODO: Ghi log lỗi căn cứ vào ex.Message và ex.StackTrace
                ModelState.AddModelError(string.Empty, "Hệ thống đang bận hoặc dữ liệu không hợp lệ. Vui lòng kiểm tra dữ liệu hoặc thử lại sau");
                return View("Edit", data);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var model = await HRDataService.GetEmployeeAsync(id);
                if (model == null)
                    return RedirectToAction("Index");
                ViewBag.CanDelete = !await HRDataService.IsUsedEmployeeAsync(id); //có dữ liệu liên quan chưa xóa được
                return View(model);
            }
            catch (Exception)
            {
                //Ghi lại log lỗi ex.Message, ex.StackTrace
                ModelState.AddModelError("Error", "Hệ thống tạm thời đang bận, vui lòng thử lại sau");
                return View("Edit", id);
            }
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await HRDataService.DeleteEmployeeAsync(id);
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                //Ghi lại log lỗi ex.Message, ex.StackTrace
                ModelState.AddModelError("Error", "Hệ thống tạm thời đang bận, vui lòng thử lại sau");
                return RedirectToAction("Delete", new { id });
            }
        }

        /// <summary>
        /// Hiển thị trang đổi mật khẩu cho nhân viên
        /// </summary>
        /// <param name="id">Mã nhân viên cần đổi mật khẩu</param>
        /// <returns>Trả về View đổi mật khẩu nhân viên</returns>
        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.EmployeeId = id;
            ViewBag.EmployeeName = model.FullName;
            return View();
        }

        /// <summary>
        /// Xử lý đổi mật khẩu cho nhân viên (admin reset)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ModelState.AddModelError(nameof(newPassword), "Vui lòng nhập mật khẩu mới");
                return await ChangePassword(id);
            }

            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null)
                return RedirectToAction("Index");

            try
            {
                var repo = new EmployeeAccountRepository(
                    ApplicationContext.Configuration?.GetConnectionString("LiteCommerceDB")
                    ?? SV22T1020591.BusinessLayers.Configuration.ConnectionString
                );

                string hashedNew = CryptHelper.HashMD5(newPassword);
                var ok = await repo.ChangePassword(employee.Email, hashedNew);

                if (!ok)
                {
                    ModelState.AddModelError("", "Không thể đổi mật khẩu cho nhân viên");
                    return await ChangePassword(id);
                }

                TempData["Message"] = "Đổi mật khẩu thành công";
                return RedirectToAction("Edit", new { id });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Lỗi hệ thống khi đổi mật khẩu");
                return await ChangePassword(id);
            }
        }

        /// <summary>
        /// Hiển thị trang thay đổi quyền của nhân viên
        /// </summary>
        /// <param name="id">Mã nhân viên cần thay đổi quyền</param>
        /// <returns>Trả về View thay đổi quyền nhân viên</returns>
        [HttpGet]
        public async Task<IActionResult> ChangeRole(int id)
        {
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            var roles = new List<SelectListItem>()
            {
                new SelectListItem(WebUserRoles.Administrator, WebUserRoles.Administrator),
                new SelectListItem(WebUserRoles.DataManager, WebUserRoles.DataManager),
                new SelectListItem(WebUserRoles.Sales, WebUserRoles.Sales)
            };

            ViewBag.Roles = roles;
            ViewBag.EmployeeId = id;
            ViewBag.EmployeeName = model.FullName;

            // Lấy role hiện tại từ DB qua HRDataService
            var roleNames = await HRDataService.GetEmployeeRoleNamesAsync(id) ?? "";
            ViewBag.CurrentRoles = string.IsNullOrEmpty(roleNames)
                ? new List<string>()
                : roleNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList();

            return View();
        }

        /// <summary>
        /// Xử lý thay đổi quyền nhân viên - hiện chưa có lưu vĩnh viễn (cần mở rộng repository và DB)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(int id, List<string>? roles)
        {
            try
            {
                var employee = await HRDataService.GetEmployeeAsync(id);
                if (employee == null) return RedirectToAction("Index");

                var roleToSave = roles != null && roles.Any()
                    ? string.Join(',', roles.Select(r => r.Trim()))
                    : string.Empty;

                var ok = await HRDataService.UpdateEmployeeRoleAsync(id, roleToSave);
                if (!ok)
                {
                    ModelState.AddModelError("", "Không thể lưu phân quyền (Update returned false)");
                    return await ChangeRole(id);
                }

                TempData["Message"] = "Đã cập nhật phân quyền";
                return RedirectToAction("Edit", new { id });
            }
            catch (Exception ex)
            {
                // Log to Debug output and show friendly message
                System.Diagnostics.Debug.WriteLine($"Error in ChangeRole: {ex}");
                ModelState.AddModelError("", "Lỗi hệ thống khi lưu phân quyền: " + ex.Message);
                return await ChangeRole(id);
            }
        }
    }
}