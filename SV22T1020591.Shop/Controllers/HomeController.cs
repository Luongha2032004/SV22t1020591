using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Catalog;

namespace SV22T1020591.Shop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        /// <summary>
        /// Khởi tạo HomeController với logger để ghi log lỗi và thông tin hệ thống.
        /// </summary>
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Trang chủ.
        /// Hiển thị danh sách sản phẩm bán chạy và sản phẩm được yêu thích.
        /// Dữ liệu được truyền qua ViewBag.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var bestSellers = await CatalogDataService.GetTopSellingProductsAsync(8);
                var favorites = await CatalogDataService.GetMostFavoritedProductsAsync(8);

                ViewBag.BestSellingProducts = bestSellers;
                ViewBag.MostFavoriteProducts = favorites;

                ViewData["Title"] = "Trang chủ";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải trang chủ");

                ViewBag.Error = "❌ Không thể tải dữ liệu trang chủ";
                return View();
            }
        }

        /// <summary>
        /// Trang giới thiệu.
        /// </summary>
        public IActionResult About()
        {
            try
            {
                ViewData["Title"] = "Giới thiệu";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải trang About");
                return Content("Lỗi: " + ex.Message);
            }
        }
    }
}