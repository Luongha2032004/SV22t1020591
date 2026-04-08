// Hiển thị ảnh được chọn từ input file lên thẻ img
// (Thẻ input có thuộc tính data-img-preview trỏ đến id của thẻ img dung để hiển thị ảnh)
function previewImage(input) {
    if (!input.files || !input.files[0]) return;

    const previewId = input.dataset.imgPreview; // lấy data-img-preview
    if (!previewId) return;

    const img = document.getElementById(previewId);
    if (!img) return;

    const reader = new FileReader();
    reader.onload = function (e) {
        img.src = e.target.result;
    };
    reader.readAsDataURL(input.files[0]);
}

// Tìm kiếm phân trang bằng AJAX
function paginationSearch(event, form, page) {
    if (event) event.preventDefault();
    if (!form) return;

    const url = form.action;
    const method = (form.method || "GET").toUpperCase();
    const targetId = form.dataset.target;

    const formData = new FormData(form);
    formData.append("page", page);

    let fetchUrl = url;
    if (method === "GET") {
        const params = new URLSearchParams(formData).toString();
        fetchUrl = url + "?" + params;
    }

    let targetEl = null;
    if (targetId) {
        targetEl = document.getElementById(targetId);
        if (targetEl) {
            targetEl.innerHTML = `
                <div class="text-center py-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-2">Đang tải dữ liệu...</p>
                </div>`;
        }
    }

    fetch(fetchUrl, {
        method: method,
        body: method === "GET" ? null : formData
    })
        .then(res => res.text())
        .then(html => {
            if (targetEl) {
                targetEl.innerHTML = html;
                // Re-execute any scripts in the loaded content
                const scripts = targetEl.querySelectorAll('script');
                scripts.forEach(script => {
                    const newScript = document.createElement('script');
                    if (script.src) {
                        newScript.src = script.src;
                    } else {
                        newScript.textContent = script.textContent;
                    }
                    document.body.appendChild(newScript);
                });
            }
        })
        .catch(() => {
            if (targetEl) {
                targetEl.innerHTML = `
                <div class="alert alert-danger text-center">
                    <i class="bi bi-exclamation-triangle-fill"></i> Không tải được dữ liệu
                </div>`;
            }
        });
}

// Mở modal và load nội dung từ link vào modal
(function () {
    //dialogModal là id của modal dùng chung được định nghĩa trong _Layout.cshtml
    const modalEl = document.getElementById("dialogModal");
    if (!modalEl) return;

    const modalContent = modalEl.querySelector(".modal-content");

    // Clear nội dung khi modal đóng
    modalEl.addEventListener('hidden.bs.modal', function () {
        modalContent.innerHTML = '';
    });

    window.openModal = function (event, link) {
        if (!link) return;
        if (event) event.preventDefault();

        let url = link;
        if (typeof link === 'object' && link.getAttribute) {
            url = link.getAttribute("href");
        }

        if (!url) return;

        // Hiển thị loading
        modalContent.innerHTML = `
            <div class="modal-body text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="mt-2">Đang tải dữ liệu...</p>
            </div>`;

        // Khởi tạo modal (chỉ tạo 1 lần)
        let modal = bootstrap.Modal.getInstance(modalEl);
        if (!modal) {
            modal = new bootstrap.Modal(modalEl, {
                backdrop: 'static',
                keyboard: false
            });
        }

        modal.show();

        // Load nội dung
        fetch(url)
            .then(res => res.text())
            .then(html => {
                modalContent.innerHTML = html;
                // Execute any scripts in the loaded content
                const scripts = modalContent.querySelectorAll('script');
                scripts.forEach(script => {
                    const newScript = document.createElement('script');
                    if (script.src) {
                        newScript.src = script.src;
                    } else {
                        newScript.textContent = script.textContent;
                    }
                    document.body.appendChild(newScript);
                });
            })
            .catch(() => {
                modalContent.innerHTML = `
                    <div class="modal-body text-center text-danger py-5">
                        <i class="bi bi-exclamation-triangle-fill fs-1"></i>
                        <p class="mt-2">Không tải được dữ liệu</p>
                        <button type="button" class="btn btn-secondary mt-3" data-bs-dismiss="modal">Đóng</button>
                    </div>`;
            });
    };
})();

// ========== GIỎ HÀNG ==========
// Recalculate totals from DOM
function recalcCartUI() {
    const cartBody = document.getElementById('cartBody');
    if (!cartBody) return;

    let totalItems = 0;
    let totalPrice = 0;

    document.querySelectorAll('#cartBody tr').forEach(tr => {
        const qtyInput = tr.querySelector('.qty-input');
        const qty = parseInt(qtyInput?.value || 0, 10);
        const priceText = tr.querySelector('td:nth-child(3)')?.textContent.trim().replace(/[^\d]/g, '') || '0';
        const price = parseFloat(priceText || 0);
        totalItems += qty;
        totalPrice += qty * price;
        const lineTotalEl = tr.querySelector('.line-total');
        if (lineTotalEl) {
            lineTotalEl.textContent = new Intl.NumberFormat('vi-VN').format(qty * price) + ' đ';
        }
    });

    const totalItemsEl = document.getElementById('cartTotalItems');
    const totalPriceEl = document.getElementById('cartTotalPrice');

    if (totalItemsEl) totalItemsEl.textContent = totalItems;
    if (totalPriceEl) totalPriceEl.innerHTML = '<strong>' + new Intl.NumberFormat('vi-VN').format(totalPrice) + ' đ</strong>';
}

// call POST /Cart/Update
async function updateOnServer(productID, quantity) {
    try {
        const fd = new FormData();
        fd.append('productID', productID);
        fd.append('quantity', quantity);
        const res = await fetch('/Cart/Update', { method: 'POST', body: fd });
        return await res.json();
    } catch (e) {
        console.error(e);
        return null;
    }
}

// change qty buttons
async function changeQty(productID, delta) {
    const tr = document.querySelector('#cartBody tr[data-productid="' + productID + '"]');
    if (!tr) return;
    const input = tr.querySelector('.qty-input');
    let qty = parseInt(input.value || 0, 10);
    qty = Math.max(1, qty + delta);
    input.value = qty;
    await updateOnServer(productID, qty);
    recalcCartUI();
    if (window.refreshCartCount) refreshCartCount();
}

// user typed qty
async function qtyChanged(productID, value) {
    let qty = parseInt(value || 0, 10);
    if (isNaN(qty) || qty < 1) qty = 1;
    const tr = document.querySelector('#cartBody tr[data-productid="' + productID + '"]');
    if (!tr) return;
    const input = tr.querySelector('.qty-input');
    input.value = qty;
    await updateOnServer(productID, qty);
    recalcCartUI();
    if (window.refreshCartCount) refreshCartCount();
}

// remove item
async function removeItem(productID) {
    if (!confirm('Bạn có chắc muốn xóa sản phẩm này khỏi giỏ?')) return;
    try {
        const fd = new FormData();
        fd.append('productID', productID);
        const res = await fetch('/Cart/Remove', { method: 'POST', body: fd });
        const data = await res.json();
        if (data && data.success) {
            const tr = document.querySelector('#cartBody tr[data-productid="' + productID + '"]');
            if (tr) tr.remove();
            recalcCartUI();
            if (window.refreshCartCount) refreshCartCount();
            if (document.querySelectorAll('#cartBody tr').length === 0) location.reload();
        } else {
            alert('Xóa không thành công.');
        }
    } catch (e) {
        console.error(e);
        alert('Lỗi khi xóa.');
    }
}

// clear cart
async function clearCart() {
    if (!confirm('Bạn có chắc muốn xóa toàn bộ giỏ hàng?')) return;
    try {
        const res = await fetch('/Cart/Clear', { method: 'POST' });
        const data = await res.json();
        if (data && data.success) {
            location.reload();
        } else {
            alert('Không thể xóa giỏ hàng.');
        }
    } catch (e) {
        console.error(e);
        alert('Lỗi khi xóa giỏ hàng.');
    }
}

// Validation checkout form
function initCheckoutValidation() {
    const form = document.getElementById('checkoutForm');
    if (!form) return;

    form.addEventListener('submit', function (e) {
        const province = document.getElementById('province')?.value.trim();
        const address = document.getElementById('address')?.value.trim();

        let isValid = true;

        if (!province) {
            const provinceEl = document.getElementById('province');
            if (provinceEl) {
                provinceEl.classList.add('is-invalid');
            }
            isValid = false;
        } else {
            const provinceEl = document.getElementById('province');
            if (provinceEl) {
                provinceEl.classList.remove('is-invalid');
            }
        }

        if (!address) {
            const addressEl = document.getElementById('address');
            if (addressEl) {
                addressEl.classList.add('is-invalid');
            }
            isValid = false;
        } else {
            const addressEl = document.getElementById('address');
            if (addressEl) {
                addressEl.classList.remove('is-invalid');
            }
        }

        if (!isValid) {
            e.preventDefault();
            showError('Vui lòng nhập đầy đủ thông tin thanh toán');
        }
    });
}

// Hiển thị thông báo lỗi
function showError(message) {
    // Kiểm tra nếu có modal error thì dùng, không thì dùng alert
    const errorModal = document.getElementById('errorModal');
    if (errorModal) {
        const errorMessage = document.getElementById('errorMessage');
        if (errorMessage) {
            errorMessage.innerHTML = message;
            const modal = new bootstrap.Modal(errorModal);
            modal.show();
        } else {
            alert(message);
        }
    } else {
        alert(message);
    }
}

// Hiển thị thông báo thành công
function showSuccess(message, orderId) {
    const successModal = document.getElementById('successModal');
    if (successModal) {
        const successMessage = document.getElementById('successMessage');
        if (successMessage) {
            successMessage.innerHTML = message + (orderId ? `<br>Mã đơn hàng: #${orderId}` : '');
            const modal = new bootstrap.Modal(successModal);
            modal.show();
        } else {
            alert(message);
        }
    } else {
        alert(message);
    }
}

// Refresh cart count on navbar
function refreshCartCount() {
    fetch('/Cart/Count')
        .then(res => res.json())
        .then(data => {
            const cartCount = document.getElementById('cartCount');
            if (cartCount) {
                cartCount.textContent = data.totalItems || 0;
            }
        })
        .catch(err => console.error('Error refreshing cart count:', err));
}

// Initialize cart functions when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    recalcCartUI();
    initCheckoutValidation();
});