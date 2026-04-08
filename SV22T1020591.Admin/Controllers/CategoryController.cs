using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020591.Admin;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Catalog;
using SV22T1020591.Models.Common;
using SV22T1020591.Admin;


/// <summary>
/// Quản lý loại hàng
/// </summary>
[Authorize]
public class CategoryController : Controller
{
    /// <summary>
    /// Hiển thị danh sách loại hàng
    /// </summary>
    /// <returns>Trả về View danh sách loại hàng</returns>
    private const string CATEGORY_SEARCH = "CategorySearchInput";
    public IActionResult Index()
    {
        var input = ApplicationContext.GetSessionData<PaginationSearchInput>(CATEGORY_SEARCH);
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
        var result = await CatalogDataService.ListCategoriesAsync(input);
        ApplicationContext.SetSessionData(CATEGORY_SEARCH, input);
        return View(result);
    }

    /// <summary>
    /// Hiển thị trang tạo mới loại hàng
    /// </summary>
    /// <returns>Trả về View nhập thông tin loại hàng</returns>
    public IActionResult Create()
    {
        ViewBag.Title = "Bổ sung loại hàng";
        var model = new Category()
        {
            CategoryID = 0
        };
        return View("Edit", model);
    }

    /// <summary>
    /// Hiển thị trang cập nhật loại hàng
    /// </summary>
    /// <param name="id">Mã loại hàng cần chỉnh sửa</param>
    /// <returns>Trả về View cập nhật loại hàng</returns>
    public async Task<IActionResult> Edit(int id)
    {
        ViewBag.Title = "Cập nhật thông tin loại hàng";
        var model = await CatalogDataService.GetCategoryAsync(id);
        if (model == null)
            return RedirectToAction("Index");
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> SaveData(Category data)
    {
        try
        {
            ViewBag.Title = data.CategoryID == 0 ? "Bổ sung loại hàng" : "Cập nhật thông tin loại hàng";

            //Kiểm tra dữ liệu đầu vào có hợp lệ không

            //Sử dụng ModelState để lưu trữ các tình huống (thông báo) lỗi và gửi thông báo lỗi cho View
            //Giả thiết: chỉ cần nhập tên, email và tỉnh thành 

            if (string.IsNullOrWhiteSpace(data.CategoryName))
                ModelState.AddModelError(nameof(data.CategoryName), "Vui lòng nhập tên loại hàng");

            if (!ModelState.IsValid)
                //Nếu có lỗi, trả về View Edit để hiển thị lỗi
                return View("Edit", data);

            //(Tùy chọn) Hiệu chỉnh dữ liệu theo quy tắc của phần mềm


            //Luu vao CSDL
            if (data.CategoryID == 0)
                await CatalogDataService.AddCategoryAsync(data);
            else
                await CatalogDataService.UpdateCategoryAsync(data);

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
    /// Hiển thị trang xác nhận xóa loại hàng
    /// </summary>
    /// <param name="id">Mã loại hàng cần xóa</param>
    /// <returns>Trả về View xác nhận xóa loại hàng</returns>
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            if (Request.Method == "POST")
            {

                await CatalogDataService.DeleteCategoryAsync(id);
                return RedirectToAction("Index");

            }
            var model = await CatalogDataService.GetCategoryAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            ViewBag.CanDelete = !await CatalogDataService.IsUsedCategoryAsync(id);
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

