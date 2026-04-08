using Microsoft.AspNetCore.Mvc;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Catalog;

namespace SV22T1020591.Shop.Controllers
{
    public class ProductController : Controller
    {
        /// <summary>
        /// Hiển thị danh sách sản phẩm.
        /// Hỗ trợ tìm kiếm, lọc theo danh mục và phân trang.
        /// </summary>
        public async Task<IActionResult> Index(ProductSearchInput input)
        {
            try
            {
                if (input.Page <= 0)
                    input.Page = 1;

                if (input.PageSize <= 0)
                    input.PageSize = 8;

                var data = await CatalogDataService.ListProductsAsync(input);

              
                var categories = await CatalogDataService.ListCategoriesAsync(new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = 0,
                    SearchValue = ""
                });

                ViewBag.Categories = categories.DataItems ?? new List<Category>();

                
                ViewBag.SearchInput = input;

                return View(data);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Hiển thị chi tiết sản phẩm.
        /// Bao gồm thông tin, hình ảnh và thuộc tính của sản phẩm.
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await CatalogDataService.GetProductAsync(id);
                if (product == null)
                    return RedirectToAction("Index");
                var photos = await CatalogDataService.ListPhotosAsync(id);
                var attributes = await CatalogDataService.ListAttributesAsync(id);

                ViewBag.Photos = photos;
                ViewBag.Attributes = attributes;

                return View(product);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }
    }
}