using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models;
using SV22T1020591.Models.Partner;
using SV22T1020591.Models.Sales;
using SV22T1020591.Shop.Models;
using SV22T1020591.Models.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SV22T1020591.Shop.Controllers
{
    public class OrderController : Controller
    {
        private const string CartSessionKeyPrefix = "Cart_";

        /// <summary>
        /// Tạo key Session cho giỏ hàng.
        /// Nếu user đăng nhập → dùng UserId.
        /// Nếu chưa → dùng Guest + ConnectionId.
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
            catch { }

            return $"{CartSessionKeyPrefix}Guest_{Request?.HttpContext?.Connection?.Id ?? Guid.NewGuid().ToString()}";
        }

        /// <summary>
        /// Lấy giỏ hàng từ Session.
        /// Có hỗ trợ key cũ "Cart" để tương thích.
        /// </summary>
        private List<CartItem> GetCart()
        {
            try
            {
                var key = GetCartSessionKey();
                var cart = ApplicationContext.GetSessionData<List<CartItem>>(key);

                if (cart == null)
                {
                    cart = ApplicationContext.GetSessionData<List<CartItem>>("Cart") ?? new List<CartItem>();
                }

                return cart;
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        /// <summary>
        /// Lấy danh sách tỉnh/thành cho dropdown.
        /// </summary>
        private async Task<List<SelectListItem>> GetProvinceSelectListAsync(string? selectedValue = null)
        {
            try
            {
                var list = new List<SelectListItem>()
                {
                    new SelectListItem() { Value = "", Text = "-- Chọn tỉnh/thành --" }
                };

                var result = await DictionaryDataService.ListProvincesAsync();
                foreach (var item in result)
                {
                    list.Add(new SelectListItem()
                    {
                        Value = item.ProvinceName,
                        Text = item.ProvinceName,
                        Selected = string.Equals(item.ProvinceName, selectedValue, StringComparison.OrdinalIgnoreCase)
                    });
                }

                return list;
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Trang xác nhận đơn hàng.
        /// Nếu giỏ hàng trống → chuyển về Cart.
        /// </summary>
        public IActionResult Create()
        {
            try
            {
                var cart = GetCart();
                if (cart.Count == 0)
                    return RedirectToAction("Index", "Cart");

                return View(cart);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Tạo đơn hàng từ giỏ hàng.
        /// Thực hiện validate và lưu Order + OrderDetails.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(string province = "", string address = "")
        {
            try
            {
                var user = User.GetUserData();
                if (user == null)
                    return RedirectToAction("Login", "Account");

                if (!int.TryParse(user.UserId, out int customerId))
                    return RedirectToAction("Login", "Account");

                var cart = GetCart();
                if (cart.Count == 0)
                    return RedirectToAction("Index", "Cart");

                if (string.IsNullOrWhiteSpace(province))
                    ModelState.AddModelError("province", "Vui lòng chọn tỉnh / thành");

                if (string.IsNullOrWhiteSpace(address))
                    ModelState.AddModelError("address", "Vui lòng nhập địa chỉ");

                if (!ModelState.IsValid)
                {
                    ViewBag.Provinces = await GetProvinceSelectListAsync(province);
                    return View("~/Views/Cart/Index.cshtml", cart);
                }

                int orderID = await SalesDataService.AddOrderAsync(customerId, province, address);

                foreach (var item in cart)
                {
                    var detail = new OrderDetail()
                    {
                        OrderID = orderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        SalePrice = item.SalePrice
                    };

                    await SalesDataService.AddDetailAsync(detail);
                }

                var cartKey = GetCartSessionKey();
                ApplicationContext.SetSessionData(cartKey, null);
                ApplicationContext.SetSessionData("Cart", null);

                return RedirectToAction("Details", new { id = orderID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ Lỗi: " + ex.Message);
                var cart = GetCart();
                ViewBag.Provinces = await GetProvinceSelectListAsync(province);
                return View("~/Views/Cart/Index.cshtml", cart);
            }
        }

        /// <summary>
        /// Danh sách đơn hàng của user hiện tại.
        /// Có phân trang và tính tổng tiền từng đơn.
        /// </summary>
        public async Task<IActionResult> Index(int page = 1)
        {
            try
            {
                int? customerId = null;

                var sessionCustomer = ApplicationContext.GetSessionData<Customer>("Customer");
                if (sessionCustomer != null)
                    customerId = sessionCustomer.CustomerID;
                else
                {
                    var user = User.GetUserData();
                    if (user != null && int.TryParse(user.UserId, out var uid))
                        customerId = uid;
                }

                if (!customerId.HasValue)
                    return RedirectToAction("Login", "Account");

                var input = new OrderSearchInput()
                {
                    Page = page,
                    PageSize = ApplicationContext.PageSize,
                };

                var raw = await SalesDataService.ListOrdersAsync(input);

                // Chuẩn bị PagedResult<OrderViewInfo> trả về View
                var result = new PagedResult<OrderViewInfo>
                {
                    Page = raw?.Page ?? input.Page,
                    PageSize = raw?.PageSize ?? input.PageSize,
                    RowCount = 0,
                    DataItems = new List<OrderViewInfo>()
                };

                // Lấy items từ raw.DataItems (chuẩn)
                IEnumerable<object> itemsEnumerable = raw?.DataItems?.Cast<object>();

                // Nếu không có DataItems, thử lấy raw.Data (tương thích các phiên bản cũ)
                if ((itemsEnumerable == null || !itemsEnumerable.Any()) && raw != null)
                {
                    var dataProp = raw.GetType().GetProperty("Data");
                    if (dataProp != null)
                    {
                        var val = dataProp.GetValue(raw) as System.Collections.IEnumerable;
                        if (val != null)
                            itemsEnumerable = val.Cast<object>();
                    }
                }

                var items = itemsEnumerable?.ToList() ?? new List<object>();

                if (items.Any())
                {
                    var viewList = new List<OrderViewInfo>();

                    foreach (var it in items)
                    {
                        if (it == null) continue;

                        // Nếu item đã là OrderViewInfo
                        if (it is OrderViewInfo ov)
                        {
                            if (ov.CustomerID == customerId.Value)
                            {
                                var details = await SalesDataService.ListDetailsAsync(ov.OrderID);
                                ov.TotalAmount = details?.Sum(d => d.Quantity * d.SalePrice) ?? 0m;
                                viewList.Add(ov);
                            }
                            continue;
                        }

                        // Nếu item là lightweight DTO (OrderSearchInfo), lấy OrderID bằng reflection
                        int orderId = 0;
                        try
                        {
                            var prop = it.GetType().GetProperty("OrderID");
                            if (prop != null)
                                orderId = Convert.ToInt32(prop.GetValue(it));
                        }
                        catch { orderId = 0; }

                        if (orderId <= 0) continue;

                        var full = await SalesDataService.GetOrderAsync(orderId);
                        if (full == null) continue;

                        var details2 = await SalesDataService.ListDetailsAsync(full.OrderID);
                        full.TotalAmount = details2?.Sum(d => d.Quantity * d.SalePrice) ?? 0m;

                        if (full.CustomerID == customerId.Value)
                            viewList.Add(full);
                    }

                    result.DataItems = viewList;

                    // Nếu class PagedResult có property Data (non-generic) — cố gắng gán để tương thích
                    var dp = result.GetType().GetProperty("Data");
                    if (dp != null && dp.PropertyType.IsAssignableFrom(typeof(List<OrderViewInfo>)))
                        dp.SetValue(result, viewList);

                    result.RowCount = viewList.Count;
                    result.Page = 1;
                }
                else
                {
                    result.RowCount = 0;
                    result.Page = 1;
                }

                return View(result);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Hiển thị chi tiết đơn hàng.
        /// Bao gồm danh sách sản phẩm và tổng tiền.
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                    return RedirectToAction("Index");

                var details = await SalesDataService.ListDetailsAsync(id);

                if (details != null)
                {
                    var total = details.Sum(d => d.Quantity * d.SalePrice);
                    if (order is OrderViewInfo ov)
                        ov.TotalAmount = total;
                }

                ViewBag.Details = details;
                return View(order);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Lịch sử đơn hàng đã hoàn thành của user.
        /// Chỉ hiển thị các đơn có trạng thái Completed.
        /// </summary>
        public async Task<IActionResult> History()
        {
            try
            {
                int? customerId = null;

                var sessionCustomer = ApplicationContext.GetSessionData<Customer>("Customer");
                if (sessionCustomer != null)
                    customerId = sessionCustomer.CustomerID;
                else
                {
                    var user = User.GetUserData();
                    if (user != null && int.TryParse(user.UserId, out var uid))
                        customerId = uid;
                }

                if (!customerId.HasValue)
                    return RedirectToAction("Login", "Account");

                var input = new OrderSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                };

                var raw = await SalesDataService.ListOrdersAsync(input);

                // Chuẩn bị danh sách phần tử (có thể ở dạng OrderViewInfo hoặc lightweight DTO)
                IEnumerable<object> itemsEnum = raw?.DataItems?.Cast<object>();
                if ((itemsEnum == null || !itemsEnum.Any()) && raw != null)
                {
                    var dataProp = raw.GetType().GetProperty("Data");
                    if (dataProp != null)
                    {
                        var val = dataProp.GetValue(raw) as System.Collections.IEnumerable;
                        if (val != null)
                            itemsEnum = val.Cast<object>();
                    }
                }

                var items = itemsEnum?.ToList() ?? new List<object>();

                var completedList = new List<OrderViewInfo>();

                foreach (var it in items)
                {
                    if (it == null) continue;

                    // Nếu đã là OrderViewInfo
                    if (it is OrderViewInfo ov)
                    {
                        if (ov.CustomerID == customerId.Value && ov.Status == OrderStatusEnum.Completed)
                        {
                            var details = await SalesDataService.ListDetailsAsync(ov.OrderID);
                            ov.TotalAmount = details?.Sum(d => d.Quantity * d.SalePrice) ?? 0m;
                            completedList.Add(ov);
                        }
                        continue;
                    }

                    // Nếu là lightweight DTO (OrderSearchInfo), lấy OrderID rồi gọi GetOrderAsync
                    int orderId = 0;
                    try
                    {
                        var prop = it.GetType().GetProperty("OrderID");
                        if (prop != null)
                            orderId = Convert.ToInt32(prop.GetValue(it));
                    }
                    catch
                    {
                        orderId = 0;
                    }

                    if (orderId <= 0) continue;

                    var full = await SalesDataService.GetOrderAsync(orderId);
                    if (full == null) continue;

                    if (full.CustomerID == customerId.Value && full.Status == OrderStatusEnum.Completed)
                    {
                        var details2 = await SalesDataService.ListDetailsAsync(full.OrderID);
                        full.TotalAmount = details2?.Sum(d => d.Quantity * d.SalePrice) ?? 0m;
                        completedList.Add(full);
                    }
                }

                return View(completedList);
            }
            catch (Exception ex)
            {
                return Content("Lỗi: " + ex.Message);
            }
        }
    }
}