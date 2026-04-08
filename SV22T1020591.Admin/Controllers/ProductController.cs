using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020591.BusinessLayers;
using SV22T1020591.Models.Catalog;

namespace SV22T1020591.Admin.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private const string PRODUCT_SEARCH = "ProductSearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(PRODUCT_SEARCH);

            if (input == null)
            {
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = "",
                    CategoryID = 0,
                    SupplierID = 0,
                    MinPrice = 0,
                    MaxPrice = 0
                };
            }

            return View(input);
        }

        public async Task<IActionResult> Search(ProductSearchInput input)
        {
            if (input.PageSize <= 0)
                input.PageSize = ApplicationContext.PageSize;

            if (input.Page <= 0)
                input.Page = 1;

            var result = await CatalogDataService.ListProductsAsync(input);
            ApplicationContext.SetSessionData(PRODUCT_SEARCH, input);

            return View(result);
        }

        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung mặt hàng";
            return View("Edit", new Product());
        }

        public async Task<IActionResult> Edit(int id = 0)
        {
            ViewBag.Title = "Cập nhật mặt hàng";

            var data = await CatalogDataService.GetProductAsync(id);
            if (data == null)
                return RedirectToAction("Index");

            ViewBag.ProductID = id;
            ViewBag.Photos = await CatalogDataService.ListPhotosAsync(id);
            ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(id);

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(Product data, IFormFile? uploadPhoto)
        {
            ViewBag.Title = data.ProductID == 0
                ? "Bổ sung mặt hàng"
                : "Cập nhật mặt hàng";

            if (string.IsNullOrWhiteSpace(data.ProductName))
                ModelState.AddModelError(nameof(data.ProductName), "Vui lòng nhập tên mặt hàng");

            if (data.CategoryID <= 0)
                ModelState.AddModelError(nameof(data.CategoryID), "Vui lòng chọn loại hàng");

            if (data.SupplierID <= 0)
                ModelState.AddModelError(nameof(data.SupplierID), "Vui lòng chọn nhà cung cấp");

            if (string.IsNullOrWhiteSpace(data.Unit))
                ModelState.AddModelError(nameof(data.Unit), "Vui lòng nhập đơn vị tính");

            if (data.Price <= 0)
                ModelState.AddModelError(nameof(data.Price), "Giá bán phải lớn hơn 0");

            if (!ModelState.IsValid)
            {
                if (data.ProductID > 0)
                {
                    ViewBag.ProductID = data.ProductID;
                    ViewBag.Photos = await CatalogDataService.ListPhotosAsync(data.ProductID);
                    ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(data.ProductID);
                }

                return View("Edit", data);
            }

            try
            {
                // Load existing product photo when editing so we don't accidentally erase it
                string? oldFileName = null;
                if (data.ProductID > 0)
                {
                    var existing = await CatalogDataService.GetProductAsync(data.ProductID);
                    oldFileName = existing?.Photo;
                    // If form didn't post Photo (null/empty), keep existing photo value
                    if (string.IsNullOrEmpty(data.Photo))
                        data.Photo = oldFileName;
                }

                string? newFileName = null;

                if (uploadPhoto != null && uploadPhoto.Length > 0)
                {
                    // Ensure folder exists
                    string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    // Create unique new file name and save file
                    newFileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    string filePath = Path.Combine(folder, newFileName);

                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }

                    // Assign the new file name to the model (do not delete old file yet)
                    data.Photo = newFileName;
                }

                // Save to database.
                // When adding a new product we need the generated ProductID to create gallery photo.
                int savedProductId;
                if (data.ProductID == 0)
                {
                    savedProductId = await CatalogDataService.AddProductAsync(data);
                    // Ensure model has ProductID for later logic and redirects
                    data.ProductID = savedProductId;
                }
                else
                {
                    await CatalogDataService.UpdateProductAsync(data);
                    savedProductId = data.ProductID;
                }

                // If a new image was uploaded, also add it to the product's photo library.
                // This makes the image visible in the gallery immediately after saving.
                if (!string.IsNullOrEmpty(newFileName))
                {
                    var photo = new ProductPhoto()
                    {
                        ProductID = savedProductId,
                        Photo = newFileName,
                        Description = string.Empty,
                        DisplayOrder = 1,
                        IsHidden = false
                    };

                    long photoId = await CatalogDataService.AddPhotoAsync(photo);
                    if (photoId <= 0)
                    {
                        // AddPhoto failed -> cleanup file and show error
                        try
                        {
                            string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", newFileName);
                            if (System.IO.File.Exists(maybePath))
                                System.IO.File.Delete(maybePath);
                        }
                        catch
                        {
                            // ignore cleanup error
                        }

                        ModelState.AddModelError("", "Ảnh đã được tải lên nhưng không lưu vào thư viện. Vui lòng thử lại.");
                        ViewBag.ProductID = savedProductId;
                        ViewBag.Photos = await CatalogDataService.ListPhotosAsync(savedProductId);
                        ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(savedProductId);
                        return View("Edit", data);
                    }

                    // Delete old product image file after gallery updated (if different)
                    if (!string.IsNullOrEmpty(oldFileName) && !string.Equals(oldFileName, newFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", oldFileName);
                            if (System.IO.File.Exists(oldPath))
                                System.IO.File.Delete(oldPath);
                        }
                        catch
                        {
                            // Ignore deletion failure
                        }
                    }
                }

                return RedirectToAction("Index");
            }
            catch
            {
                // If an exception occurred and we already saved a new uploaded file, attempt to remove it to avoid orphan files
                try
                {
                    // new uploaded file name is now in data.Photo only if a new file was uploaded.
                    // Attempt deleting that file to avoid leaving orphaned files.
                    if (!string.IsNullOrEmpty(data.Photo))
                    {
                        string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", data.Photo);
                        if (System.IO.File.Exists(maybePath))
                            System.IO.File.Delete(maybePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                ModelState.AddModelError("Error", "Hệ thống tạm thời bận, vui lòng thử lại sau");

                if (data.ProductID > 0)
                {
                    ViewBag.ProductID = data.ProductID;
                    ViewBag.Photos = await CatalogDataService.ListPhotosAsync(data.ProductID);
                    ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(data.ProductID);
                }

                return View("Edit", data);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id = 0)
        {
            var data = await CatalogDataService.GetProductAsync(id);
            if (data == null)
                return RedirectToAction("Index");

            ViewBag.CanDelete = !await CatalogDataService.IsUsedProductAsync(id);
            return View(data);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var data = await CatalogDataService.GetProductAsync(id);
                if (data == null)
                    return RedirectToAction("Index");

                // If product is used by other records, do not delete
                if (await CatalogDataService.IsUsedProductAsync(id))
                {
                    ModelState.AddModelError("", "Mặt hàng không thể xóa vì đã có dữ liệu liên quan.");
                    ViewBag.CanDelete = false;
                    return View("Delete", data);
                }

                // 1) Read gallery entries for cleanup
                var gallery = await CatalogDataService.ListPhotosAsync(id);

                // 2) Delete gallery records and their files (best-effort per item)
                if (gallery != null)
                {
                    foreach (var p in gallery)
                    {
                        try
                        {
                            var removed = await CatalogDataService.DeletePhotoAsync(p.PhotoID);
                            if (removed && !string.IsNullOrEmpty(p.Photo))
                            {
                                try
                                {
                                    string galleryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", p.Photo);
                                    if (System.IO.File.Exists(galleryPath))
                                        System.IO.File.Delete(galleryPath);
                                }
                                catch
                                {
                                    // ignore individual file deletion errors
                                }
                            }
                        }
                        catch
                        {
                            // ignore per-photo delete errors to continue attempting others
                        }
                    }
                }

                // 3) Delete product record
                var deleted = await CatalogDataService.DeleteProductAsync(id);
                if (!deleted)
                {
                    ModelState.AddModelError("", "Không thể xóa mặt hàng. Vui lòng thử lại sau.");
                    ViewBag.CanDelete = !await CatalogDataService.IsUsedProductAsync(id);
                    return View("Delete", data);
                }

                // 4) Delete main product photo file (if any)
                if (!string.IsNullOrEmpty(data.Photo))
                {
                    try
                    {
                        string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", data.Photo);
                        if (System.IO.File.Exists(photoPath))
                            System.IO.File.Delete(photoPath);
                    }
                    catch
                    {
                        // ignore file deletion errors
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var data = await CatalogDataService.GetProductAsync(id);
                ViewBag.CanDelete = data != null && !await CatalogDataService.IsUsedProductAsync(id);
                ModelState.AddModelError("", "Lỗi khi xóa mặt hàng: " + ex.Message);
                return View("Delete", data);
            }
        }

        [HttpGet]
        public IActionResult CreatePhoto(int productId)
        {
            ViewBag.Title = "Thêm ảnh";
            ViewBag.ProductID = productId;

            // id = ProductID
            var model = new SV22T1020591.Models.Catalog.ProductPhoto()
            {
                PhotoID = 0,
                ProductID = productId,
                Description = string.Empty,
                DisplayOrder = 1,
                IsHidden = false,
                Photo = string.Empty
            };

            // Reuse the EditPhoto view
            return View("EditPhoto", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePhoto(ProductPhoto data, IFormFile? uploadPhoto)
        {
            try
            {
                // Ensure non-nullable DB columns are populated
                data.Description ??= string.Empty;
                if (data.DisplayOrder <= 0) data.DisplayOrder = 1;

                // capture existing photo filename when editing
                string? existingPhotoFileName = null;
                if (data.PhotoID > 0)
                {
                    var existingPhoto = await CatalogDataService.GetPhotoAsync(data.PhotoID);
                    existingPhotoFileName = existingPhoto?.Photo;
                }

                // Save uploaded file (if any)
                string? newFileName = null;
                if (uploadPhoto != null && uploadPhoto.Length > 0)
                {
                    var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    newFileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    var filePath = Path.Combine(folder, newFileName);
                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }

                    data.Photo = newFileName;
                }

                if (data.PhotoID == 0)
                {
                    // Add new gallery photo
                    var newId = await CatalogDataService.AddPhotoAsync(data);
                    System.Diagnostics.Debug.WriteLine($"CatalogDataService.AddPhotoAsync returned: {newId} for ProductID={data.ProductID}, Photo='{data.Photo}'");

                    if (newId <= 0)
                    {
                        // cleanup file if added but DB insert failed
                        try
                        {
                            if (!string.IsNullOrEmpty(data.Photo))
                            {
                                string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", data.Photo);
                                if (System.IO.File.Exists(maybePath)) System.IO.File.Delete(maybePath);
                            }
                        }
                        catch { }

                        ModelState.AddModelError("", "Không thể lưu ảnh vào thư viện. Vui lòng thử lại.");
                        return View("EditPhoto", data);
                    }

                    // Optionally update product main photo to the newly added photo
                    try
                    {
                        var product = await CatalogDataService.GetProductAsync(data.ProductID);
                        if (product != null)
                        {
                            product.Photo = data.Photo ?? string.Empty;
                            await CatalogDataService.UpdateProductAsync(product);
                        }
                    }
                    catch
                    {
                        // ignore product update failure (gallery is saved)
                    }
                }
                else
                {
                    // Update existing gallery photo
                    // make a defensive copy to ensure Description/DisplayOrder set
                    var toUpdate = data;
                    toUpdate.Description ??= string.Empty;
                    if (toUpdate.DisplayOrder <= 0) toUpdate.DisplayOrder = 1;

                    var ok = await CatalogDataService.UpdatePhotoAsync(toUpdate);
                    System.Diagnostics.Debug.WriteLine($"CatalogDataService.UpdatePhotoAsync returned: {ok} for PhotoID={data.PhotoID}");
                    if (!ok)
                    {
                        // cleanup uploaded file if present
                        try
                        {
                            if (!string.IsNullOrEmpty(data.Photo) && !string.Equals(data.Photo, existingPhotoFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", data.Photo);
                                if (System.IO.File.Exists(maybePath)) System.IO.File.Delete(maybePath);
                            }
                        }
                        catch { }

                        ModelState.AddModelError("", "Không thể cập nhật thông tin ảnh. Vui lòng thử lại.");
                        return View("EditPhoto", data);
                    }

                    // If this gallery photo was the product's main photo, update product.Photo to the new filename
                    try
                    {
                        if (!string.IsNullOrEmpty(existingPhotoFileName) && !string.Equals(existingPhotoFileName, data.Photo, StringComparison.OrdinalIgnoreCase))
                        {
                            var product = await CatalogDataService.GetProductAsync(data.ProductID);
                            if (product != null && string.Equals(product.Photo, existingPhotoFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                product.Photo = data.Photo;
                                await CatalogDataService.UpdateProductAsync(product);

                                // delete old file after successful update
                                try
                                {
                                    string oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", existingPhotoFileName);
                                    if (System.IO.File.Exists(oldPath))
                                        System.IO.File.Delete(oldPath);
                                }
                                catch
                                {
                                    // ignore deletion failure
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore product update failure
                    }
                }

                return Redirect($"/Product/Edit/{data.ProductID}#photos");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SavePhoto exception: " + ex.ToString());
                // cleanup new uploaded file if present
                try
                {
                    if (!string.IsNullOrEmpty(data.Photo))
                    {
                        string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", data.Photo);
                        if (System.IO.File.Exists(maybePath))
                            System.IO.File.Delete(maybePath);
                    }
                }
                catch { }

                ModelState.AddModelError("", "Lỗi khi lưu ảnh: " + ex.Message);
                return View("EditPhoto", data);
            }
        }

        /// <summary>
        /// Hiển thị trang xác nhận xóa hình ảnh của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng chứa hình ảnh</param>
        /// <param name="photoId">Mã hình ảnh cần xóa</param>
        /// <returns>Trả về View xác nhận xóa hình ảnh</returns>
        public async Task<IActionResult> DeletePhoto(int productId, int photoId)
        {
            await CatalogDataService.DeletePhotoAsync(photoId);
            return Redirect($"/Product/Edit/{productId}#photos");
        }

        [HttpPost]
        public async Task<IActionResult> UploadPhoto(int productId, IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return Json(new { success = false, message = "Chưa có file ảnh" });

            string? fileName = null;
            try
            {
                fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var path = Path.Combine(folder, fileName);
                await using (var stream = new FileStream(path, FileMode.Create))
                {
                    await photo.CopyToAsync(stream);
                }

                // Gọi service để cập nhật Photo cho product (this also adds gallery entry)
                var ok = await CatalogDataService.SetProductPhotoAsync(productId, fileName);
                if (!ok)
                {
                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    return Json(new { success = false, message = "Không thể lưu ảnh vào cơ sở dữ liệu" });
                }

                var url = Url.Content($"~/images/products/{fileName}");
                return Json(new { success = true, fileName, url });
            }
            catch (Exception ex)
            {
                // cleanup if file was created
                try
                {
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string maybePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", fileName);
                        if (System.IO.File.Exists(maybePath))
                            System.IO.File.Delete(maybePath);
                    }
                }
                catch { }

                return Json(new { success = false, message = ex.Message });
            }
        }
        public async Task<IActionResult> EditPhoto(int productId, int photoId)
        {
            var data = await CatalogDataService.GetPhotoAsync(photoId);
            if (data == null)
                return RedirectToAction("Edit", new { id = productId });

            return View(data);
        }
    }
}