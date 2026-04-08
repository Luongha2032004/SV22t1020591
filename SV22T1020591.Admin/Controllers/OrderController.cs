using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SV22T1020591.Admin;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Catalog;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Sales;
using SV22T1020591.Models.Partner;

namespace SV22T1020591.Admin.Controllers
{
    [Authorize(Roles = WebUserRoles.Sales + "," + WebUserRoles.Administrator)]
    public class OrderController : Controller
    {
        private const int PAGE_SIZE = 10;
        private const string ORDER_SEARCH = "OrderSearchInput";
        private const string SEARCH_PRODUCT = "SearchProductToSale";

        // ================= DANH SÁCH =================
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<OrderSearchInput>(ORDER_SEARCH);

            if (input == null)
            {
                input = new OrderSearchInput()
                {
                    Page = 1,
                    PageSize = PAGE_SIZE,
                    SearchValue = "",
                    Status = 0
                };
            }

            return View(input);
        }

        public async Task<IActionResult> SearchResult(OrderSearchInput input)
        {
            if (input == null)
                input = new OrderSearchInput();

            if (input.Page <= 0) input.Page = 1;
            if (input.PageSize <= 0) input.PageSize = PAGE_SIZE;
            input.SearchValue ??= "";

            ApplicationContext.SetSessionData(ORDER_SEARCH, input);

            var result = await SalesDataService.ListOrdersAsync(input);

            if (result != null && result.DataItems != null)
            {
                foreach (var item in result.DataItems)
                {
                    var details = await SalesDataService.ListDetailsAsync(item.OrderID);
                    item.SumOfPrice = details.Sum(x => x.Quantity * x.SalePrice);
                }
            }

            return PartialView("SearchResult", result);
        }

        // ================= CHI TIẾT =================
        public async Task<IActionResult> Detail(int id)
        {
            var data = await SalesDataService.GetOrderAsync(id);
            if (data == null)
                return RedirectToAction("Index");

            ViewBag.OrderDetails = await SalesDataService.ListDetailsAsync(id);

            var shippers = await PartnerDataService.ListShippersAsync(
                new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = 100
                });

            ViewBag.Shippers = new SelectList(shippers.DataItems, "ShipperID", "ShipperName");

            return View(data);
        }

        // ================= TẠO ĐƠN HÀNG =================
        public async Task<IActionResult> Create()
        {
            ShoppingCartService.ClearCart();

            var input = ApplicationContext.GetSessionData<ProductSearchInput>(SEARCH_PRODUCT)
                        ?? new ProductSearchInput()
                        {
                            Page = 1,
                            PageSize = 5,
                            SearchValue = ""
                        };

            var customerInput = new PaginationSearchInput() { Page = 1, PageSize = 200 };
            var customers = await PartnerDataService.ListCustomersAsync(customerInput);

            var customerList = customers?.DataItems ?? new List<Customer>();
            ViewBag.Customers = new SelectList(customerList, "CustomerID", "CustomerName");

            return View(input);
        }

        public async Task<IActionResult> SearchProduct(ProductSearchInput input)
        {
            input ??= new ProductSearchInput() { Page = 1, PageSize = 3 };

            var result = await CatalogDataService.ListProductsAsync(input);
            ApplicationContext.SetSessionData(SEARCH_PRODUCT, input);

            return PartialView("SearchProduct", result);
        }

        // ================= GIỎ HÀNG =================
        public IActionResult ShowCart()
        {
            var cart = ShoppingCartService.GetShoppingCart();
            return PartialView("ShowCart", cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddCartItem(int productID, int quantity, decimal price)
        {
            var product = await CatalogDataService.GetProductAsync(productID);

            if (product == null)
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });

            ShoppingCartService.AddCartItem(new OrderDetailViewInfo()
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Unit = product.Unit,
                Photo = product.Photo ?? "nophoto.png",
                Quantity = quantity,
                SalePrice = price
            });

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateCartItem(int productID, int quantity, decimal salePrice)
        {
            ShoppingCartService.UpdateCartItem(productID, quantity, salePrice);
            return Json(new { success = true });
        }

        // ================= XÓA MẶT HÀNG KHỎI GIỎ =================
        // GET: returns the confirmation modal (GET only)
        [HttpGet]
        public IActionResult DeleteCartItem(int productID = 0)
        {
            ViewBag.ProductID = productID;
            return PartialView("DeleteCartItem");
        }

        // POST: perform the deletion and return JSON
        [HttpPost]
        [ActionName("DeleteCartItem")]
        public IActionResult DeleteCartItemConfirmed(int productID = 0)
        {
            ShoppingCartService.RemoveCartItem(productID);
            return Json(new ApiResult(1, "Xóa mặt hàng khỏi giỏ thành công"));
        }

        [HttpGet]
        public IActionResult ClearCart()
        {
            // Return the confirmation modal partial
            return PartialView("ClearCart");
        }

        [HttpPost]
        [ActionName("ClearCart")]
        public IActionResult ClearCartConfirmed()
        {
            ShoppingCartService.ClearCart();
            return Json(new ApiResult(1, "Xóa giỏ hàng thành công"));
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(int customerID, string province, string address)
        {
            var cart = ShoppingCartService.GetShoppingCart();

            if (cart == null || cart.Count == 0)
                return Json(new { code = 0, message = "Giỏ hàng đang trống" });

            int orderID = await SalesDataService.AddOrderAsync(customerID, province, address);

            foreach (var item in cart)
            {
                await SalesDataService.AddDetailAsync(new OrderDetail()
                {
                    OrderID = orderID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    SalePrice = item.SalePrice
                });
            }

            ShoppingCartService.ClearCart();

            return Json(new
            {
                success = true,
                orderID,
                redirectUrl = Url.Action("Detail", new { id = orderID })
            });
        }

        // ================= XỬ LÝ ĐƠN HÀNG =================

        // GET: Accept modal
        public IActionResult Accept(int id)
        {
            ViewBag.OrderID = id;
            return PartialView("Accept");
        }

        // POST: Accept order (chuyển từ New -> Accepted)
        [HttpPost]
        public async Task<IActionResult> AcceptConfirmed(int id)
        {
            try
            {
                // Kiểm tra đăng nhập
                var user = User.GetUserData();
                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn chưa đăng nhập hoặc phiên làm việc đã hết hạn"
                    });
                }

                if (!int.TryParse(user.UserId, out var employeeID))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không xác định được mã nhân viên"
                    });
                }

                // Lấy thông tin đơn hàng trước khi xử lý
                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy đơn hàng có mã {id}"
                    });
                }

                // Kiểm tra trạng thái hiện tại
                if (order.Status != OrderStatusEnum.New)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Đơn hàng đang ở trạng thái {order.Status}. Chỉ có thể duyệt đơn ở trạng thái New."
                    });
                }

                // Thực hiện duyệt đơn
                var result = await SalesDataService.AcceptOrderAsync(id, employeeID);

                if (!result)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể duyệt đơn hàng. Vui lòng thử lại sau."
                    });
                }

                // Kiểm tra lại trạng thái sau khi cập nhật
                var updatedOrder = await SalesDataService.GetOrderAsync(id);

                return Json(new
                {
                    success = true,
                    orderId = id,
                    oldStatus = order.Status.ToString(),
                    newStatus = updatedOrder?.Status.ToString() ?? "Unknown",
                    message = "Đơn hàng đã được duyệt thành công",
                    redirectUrl = Url.Action("Detail", new { id })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AcceptConfirmed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return Json(new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // GET: Shipping modal
        public async Task<IActionResult> Shipping(int id)
        {
            ViewBag.OrderID = id;

            var shipperInput = new PaginationSearchInput() { Page = 1, PageSize = 200 };
            var shippers = await PartnerDataService.ListShippersAsync(shipperInput);
            var shipperList = shippers?.DataItems ?? new List<Shipper>();
            ViewBag.Shippers = new SelectList(shipperList, "ShipperID", "ShipperName");

            return PartialView("Shipping");
        }

        // POST: Shipping order (chuyển từ Accepted -> Shipping)
        [HttpPost]
        public async Task<IActionResult> ShipConfirmed(int id, int shipperID)
        {
            try
            {
                // Kiểm tra đăng nhập
                var user = User.GetUserData();
                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn chưa đăng nhập hoặc phiên làm việc đã hết hạn"
                    });
                }

                // Lấy thông tin đơn hàng trước khi xử lý
                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy đơn hàng có mã {id}"
                    });
                }

                // Kiểm tra trạng thái hiện tại
                if (order.Status != OrderStatusEnum.Accepted)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Đơn hàng đang ở trạng thái {order.Status}. Chỉ có thể chuyển giao đơn ở trạng thái Accepted."
                    });
                }

                // Kiểm tra shipper
                if (shipperID <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Vui lòng chọn người giao hàng"
                    });
                }

                // Thực hiện chuyển giao
                var result = await SalesDataService.ShipOrderAsync(id, shipperID);

                if (!result)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể chuyển giao đơn hàng. Vui lòng thử lại sau."
                    });
                }

                // Kiểm tra lại trạng thái sau khi cập nhật
                var updatedOrder = await SalesDataService.GetOrderAsync(id);

                return Json(new
                {
                    success = true,
                    orderId = id,
                    oldStatus = order.Status.ToString(),
                    newStatus = updatedOrder?.Status.ToString() ?? "Unknown",
                    message = "Đơn hàng đã được chuyển giao thành công",
                    redirectUrl = Url.Action("Detail", new { id })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShipConfirmed: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // GET: Finish modal
        public IActionResult Finish(int id)
        {
            ViewBag.OrderID = id;
            return PartialView("Finish");
        }

        // POST: Complete order (chuyển từ Shipping -> Completed)
        [HttpPost]
        public async Task<IActionResult> FinishConfirmed(int id)
        {
            try
            {
                // Lấy thông tin đơn hàng trước khi xử lý
                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy đơn hàng có mã {id}"
                    });
                }

                // Kiểm tra trạng thái hiện tại
                if (order.Status != OrderStatusEnum.Shipping)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Đơn hàng đang ở trạng thái {order.Status}. Chỉ có thể hoàn tất đơn ở trạng thái Shipping."
                    });
                }

                // Thực hiện hoàn tất
                var result = await SalesDataService.CompleteOrderAsync(id);

                if (!result)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể hoàn tất đơn hàng. Vui lòng thử lại sau."
                    });
                }

                // Kiểm tra lại trạng thái sau khi cập nhật
                var updatedOrder = await SalesDataService.GetOrderAsync(id);

                return Json(new
                {
                    success = true,
                    orderId = id,
                    oldStatus = order.Status.ToString(),
                    newStatus = updatedOrder?.Status.ToString() ?? "Unknown",
                    message = "Đơn hàng đã hoàn tất thành công",
                    redirectUrl = Url.Action("Detail", new { id })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FinishConfirmed: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // GET: Reject confirmation modal (show modal only)
        public IActionResult Reject(int id)
        {
            ViewBag.OrderID = id;
            return PartialView("Reject");
        }

        // POST: Reject order (perform the rejection)
        [HttpPost]
        public async Task<IActionResult> RejectConfirmed(int id)
        {
            try
            {
                var user = User.GetUserData();
                if (user == null || !int.TryParse(user.UserId, out int employeeID))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn chưa đăng nhập hoặc không xác thực được người dùng"
                    });
                }

                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy đơn hàng có mã {id}"
                    });
                }

                if (order.Status != OrderStatusEnum.New)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Chỉ có thể từ chối đơn hàng ở trạng thái New. Trạng thái hiện tại: {order.Status}"
                    });
                }

                var ok = await SalesDataService.RejectOrderAsync(id, employeeID);
                if (!ok)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể từ chối đơn hàng. Vui lòng thử lại sau."
                    });
                }

                var updated = await SalesDataService.GetOrderAsync(id);
                return Json(new
                {
                    success = true,
                    orderId = id,
                    oldStatus = order.Status.ToString(),
                    newStatus = updated?.Status.ToString() ?? "Unknown",
                    message = "Đã từ chối đơn hàng",
                    redirectUrl = Url.Action("Detail", new { id })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RejectConfirmed: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // POST: Delete order (improved error handling / logging)
        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy đơn hàng"
                    });
                }

                if (order.Status != OrderStatusEnum.New && order.Status != OrderStatusEnum.Rejected)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Chỉ có thể xóa đơn hàng ở trạng thái New hoặc Rejected. Trạng thái hiện tại: {order.Status}"
                    });
                }

                var result = await SalesDataService.DeleteOrderAsync(id);
                if (!result)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể xóa đơn hàng. Vui lòng thử lại sau."
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "Đã xóa đơn hàng thành công",
                    redirectUrl = Url.Action("Index")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteConfirmed: {ex}");
                return Json(new
                {
                    success = false,
                    message = $"Lỗi: {ex.Message}"
                });
            }
        }

        // POST: Cancel order (hủy đơn hàng)
        [HttpPost]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            try
            {
                var user = User.GetUserData();
                if (user == null || !int.TryParse(user.UserId, out int employeeID))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn chưa đăng nhập hoặc không xác thực được người dùng"
                    });
                }

                var order = await SalesDataService.GetOrderAsync(id);
                if (order == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy đơn hàng có mã {id}"
                    });
                }

                // Prevent cancelling already finalised orders
                if (order.Status == OrderStatusEnum.Completed || order.Status == OrderStatusEnum.Cancelled || order.Status == OrderStatusEnum.Rejected)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không thể hủy đơn hàng ở trạng thái hiện tại: {order.Status}"
                    });
                }

                var ok = await SalesDataService.CancelOrderAsync(id);
                if (!ok)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không thể hủy đơn hàng. Vui lòng thử lại sau."
                    });
                }

                var updated = await SalesDataService.GetOrderAsync(id);
                return Json(new
                {
                    success = true,
                    orderId = id,
                    oldStatus = order.Status.ToString(),
                    newStatus = updated?.Status.ToString() ?? "Unknown",
                    message = "Đã hủy đơn hàng",
                    redirectUrl = Url.Action("Detail", new { id })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CancelConfirmed: {ex}");
                return Json(new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // Replace the existing Cancel action so the modal partial is returned with the order id.
        public IActionResult Cancel(int id = 0)
        {
            ViewBag.OrderID = id;
            return PartialView("Cancel");
        }

        [HttpPost]
        public async Task<IActionResult> Search(OrderSearchInput input)
        {
            // Support date strings from UI date-picker (formats: dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd, yyyy/MM/dd)
            // If model binding didn't parse DateFrom/DateTo (common when UI returns dd/MM/yyyy), try to parse from query.
            try
            {
                var dfRaw = Request.Query["DateFrom"].ToString();
                var dtRaw = Request.Query["DateTo"].ToString();

                if (string.IsNullOrWhiteSpace(dfRaw) == false && input.DateFrom == null)
                {
                    if (DateTime.TryParseExact(dfRaw,
                                               new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd" },
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None,
                                               out var parsedFrom))
                    {
                        input.DateFrom = parsedFrom.Date;
                    }
                    else if (DateTime.TryParse(dfRaw, out parsedFrom))
                    {
                        input.DateFrom = parsedFrom.Date;
                    }
                }

                if (string.IsNullOrWhiteSpace(dtRaw) == false && input.DateTo == null)
                {
                    if (DateTime.TryParseExact(dtRaw,
                                               new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd" },
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None,
                                               out var parsedTo))
                    {
                        input.DateTo = parsedTo.Date;
                    }
                    else if (DateTime.TryParse(dtRaw, out parsedTo))
                    {
                        input.DateTo = parsedTo.Date;
                    }
                }
            }
            catch
            {
                // ignore parsing errors; fall back to whatever model binder provided
            }

            await Task.Delay(100);
            var result = await SalesDataService.ListOrdersAsync(input);
            ApplicationContext.SetSessionData(ORDER_SEARCH, input);
            return View(result);
        }
    }
}