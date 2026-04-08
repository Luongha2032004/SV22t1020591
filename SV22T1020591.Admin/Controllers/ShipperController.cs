using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020591.Admin;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Partner;


namespace SV22T1020591.Admin.Controllers
{
    /// <summary>
    /// Quản lý đơn vị giao hàng
    /// </summary>
    [Authorize]
    public class ShipperController : Controller
    {
        private const string SHIPPER_SEARCH = "ShipperSearchInput";
        /// <summary>
        /// Hiển thị danh sách đơn vị giao hàng
        /// </summary>
        /// <returns>Trả về View danh sách đơn vị giao hàng</returns>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(SHIPPER_SEARCH);
            if (input == null)
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };
            return View(input);
        }
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {

            var result = await PartnerDataService.ListShippersAsync(input);
            ApplicationContext.SetSessionData(SHIPPER_SEARCH, input);
            return View(result);
        }
        /// <summary>
        /// Hiển thị trang tạo mới đơn vị giao hàng
        /// </summary>
        /// <returns>Trả về View nhập thông tin đơn vị giao hàng</returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung người giao hàng";
            var model = new Shipper()
            {
                ShipperID = 0
            };
            return View("Edit", model);

        }

        /// <summary>
        /// Hiển thị trang cập nhật đơn vị giao hàng
        /// </summary>
        /// <param name="id">Mã đơn vị giao hàng cần chỉnh sửa</param>
        /// <returns>Trả về View cập nhật đơn vị giao hàng</returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin người giao hàng";
            var model = await PartnerDataService.GetShipperAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SaveData(Shipper data)
        {
            try
            {
                ViewBag.Title = data.ShipperID == 0 ? "Bổ sung người giao hàng" : "Cập nhật thông tin người giao hàng";

                //Kiểm tra dữ liệu đầu vào có hợp lệ không

                //Sử dụng ModelState để lưu trữ các tình huống (thông báo) lỗi và gửi thông báo lỗi cho View
                //Giả thiết: chỉ cần nhập tên, email và tỉnh thành 

                if (string.IsNullOrWhiteSpace(data.ShipperName))
                    ModelState.AddModelError(nameof(data.ShipperName), "Vui lòng nhập tên người giao hàng");

                if (string.IsNullOrEmpty(data.Phone))
                    ModelState.AddModelError(nameof(data.Phone), "Vui lòng nhập số điện thoại");

                if (!ModelState.IsValid)
                    //Nếu có lỗi, trả về View Edit để hiển thị lỗi
                    return View("Edit", data);

                //(Tùy chọn) Hiệu chỉnh dữ liệu theo quy tắc của phần mềm

                //Luu vao CSDL
                if (data.ShipperID == 0)
                    await PartnerDataService.AddShipperAsync(data);
                else
                    await PartnerDataService.UpdateShipperAsync(data);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                //Ghi lại log lỗi ex.Message, ex.StackTrace
                ModelState.AddModelError("Errol", "Hệ thống tạm thời đang bận, vui lòng thử lại sau");
                return View("Edit", data);
            }
        }
        /// <summary>
        /// Hiển thị trang xác nhận xóa đơn vị giao hàng
        /// </summary>
        /// <param name="id">Mã đơn vị giao hàng cần xóa</param>
        /// <returns>Trả về View xác nhận xóa đơn vị giao hàng</returns>
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (Request.Method == "POST")
                {

                    await PartnerDataService.DeleteShipperAsync(id);
                    return RedirectToAction("Index");

                }
                var model = await PartnerDataService.GetShipperAsync(id);
                if (model == null)
                    return RedirectToAction("Index");
                ViewBag.CanDelete = !await PartnerDataService.IsUsedShipperAsync(id);
                return View(model);
            }
            catch (Exception ex)
            {
                //Ghi lại log lỗi ex.Message, ex.StackTrace
                ModelState.AddModelError("Errol", "Hệ thống tạm thời đang bận, vui lòng thử lại sau");
                return View("Edit", id);
            }
        }
    }
}


