using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SV22T1020591.Shop.Models;
using SV22T1020591.BusinessLayers;


namespace SV22T1020591.Shop.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKeyPrefix = "Cart_";

        /// <summary>
        /// Tạo key Session cho giỏ hàng của user hiện tại.
        /// Nếu đã đăng nhập → dùng UserId.
        /// Nếu chưa đăng nhập → dùng Guest + ConnectionId.
        /// </summary>
        private string GetCartSessionKey()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var user = User.GetUserData();
                    if (user != null && !string.IsNullOrEmpty(user.UserId))
                        return $"{CartSessionKeyPrefix}{user.UserId}";
                }
            }
            catch
            {
        
            }

            return $"{CartSessionKeyPrefix}Guest_{Request?.HttpContext?.Connection?.Id ?? Guid.NewGuid().ToString()}";
        }

        /// <summary>
        /// Lấy danh sách giỏ hàng từ Session.
        /// Nếu chưa có sẽ khởi tạo mới.
        /// </summary>
        private List<CartItem> GetCart()
        {
            try
            {
                var key = GetCartSessionKey();
                var cart = ApplicationContext.GetSessionData<List<CartItem>>(key);

                if (cart == null)
                {
                    cart = new List<CartItem>();
                    ApplicationContext.SetSessionData(key, cart);
                }

                return cart;
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        /// <summary>
        /// Lưu giỏ hàng vào Session.
        /// </summary>
        private void SaveCart(List<CartItem> cart)
        {
            try
            {
                var key = GetCartSessionKey();
                ApplicationContext.SetSessionData(key, cart ?? new List<CartItem>());
            }
            catch
            {
            }
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng hiện tại trong Session.
        /// </summary>
        private void ClearCurrentCart()
        {
            try
            {
                var key = GetCartSessionKey();
                ApplicationContext.SetSessionData(key, null);
            }
            catch
            {
              
            }
        }

        /// <summary>
        /// Hiển thị trang giỏ hàng.
        /// Đồng thời load danh sách tỉnh/thành cho dropdown.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var items = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "-- Chọn tỉnh/thành --" }
                };

                var provinces = await DictionaryDataService.ListProvincesAsync();
                if (provinces != null)
                {
                    foreach (var p in provinces)
                    {
                        items.Add(new SelectListItem
                        {
                            Value = p.ProvinceName,
                            Text = p.ProvinceName
                        });
                    }
                }

                ViewBag.Provinces = items;

                var cart = GetCart();
                return View(cart);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng (AJAX).
        /// Nếu sản phẩm đã tồn tại → tăng số lượng.
        /// Yêu cầu người dùng phải đăng nhập.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productID, int quantity = 1)
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    var returnUrl = (Request.Path + Request.QueryString).ToString();
                    var loginUrl = Url.Action("Login", "Account", new { returnUrl }) ?? "/Account/Login";
                    return Json(new { requiresLogin = true, loginUrl });
                }

                if (productID <= 0 || quantity <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var cart = GetCart();
                var item = cart.FirstOrDefault(x => x.ProductID == productID);

                if (item == null)
                {
                    var product = await CatalogDataService.GetProductAsync(productID);
                    if (product == null)
                        return Json(new { success = false, message = "Sản phẩm không tồn tại" });

                    cart.Add(new CartItem
                    {
                        ProductID = product.ProductID,
                        ProductName = product.ProductName,
                        SalePrice = product.Price,
                        Quantity = quantity
                    });
                }
                else
                {
                    item.Quantity += quantity;
                }

                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    totalItems = cart.Sum(x => x.Quantity)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy tổng số lượng sản phẩm trong giỏ (AJAX).
        /// </summary>
        [HttpGet]
        public IActionResult Count()
        {
            try
            {
                var cart = GetCart();
                return Json(new { totalItems = cart.Sum(x => x.Quantity) });
            }
            catch (Exception ex)
            {
                return Json(new { totalItems = 0, error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật số lượng sản phẩm trong giỏ (AJAX).
        /// Nếu số lượng <= 0 thì xóa sản phẩm khỏi giỏ.
        /// </summary>
        [HttpPost]
        public IActionResult Update(int productID, int quantity)
        {
            try
            {
                if (productID <= 0)
                    return Json(new { success = false, message = "productID không hợp lệ" });

                var cart = GetCart();
                var item = cart.FirstOrDefault(x => x.ProductID == productID);

                if (item != null)
                {
                    if (quantity <= 0)
                        cart.RemoveAll(x => x.ProductID == productID);
                    else
                        item.Quantity = quantity;
                }

                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    totalItems = cart.Sum(x => x.Quantity),
                    subtotal = cart.Sum(x => x.SalePrice * x.Quantity)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa một sản phẩm khỏi giỏ hàng (AJAX).
        /// </summary>
        [HttpPost]
        public IActionResult Remove(int productID)
        {
            try
            {
                if (productID <= 0)
                    return Json(new { success = false, message = "productID không hợp lệ" });

                var cart = GetCart();
                cart.RemoveAll(x => x.ProductID == productID);

                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    totalItems = cart.Sum(x => x.Quantity),
                    subtotal = cart.Sum(x => x.SalePrice * x.Quantity)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng (AJAX).
        /// </summary>
        [HttpPost]
        public IActionResult Clear()
        {
            try
            {
                ClearCurrentCart();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}