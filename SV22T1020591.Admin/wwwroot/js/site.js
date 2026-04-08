// ================= MODAL HANDLER CHUẨN =================
(function () {
    const modalEl = document.getElementById("dialogModal");
    if (!modalEl) return;

    const modalContent = modalEl.querySelector(".modal-content");

    // XÓA nội dung khi đóng
    modalEl.addEventListener('hidden.bs.modal', function () {
        modalContent.innerHTML = "";
    });

    // ================= MỞ MODAL =================
    window.openModal = function (event, linkOrUrl, orderId = null) {
        let url;

        if (typeof linkOrUrl === 'string') {
            url = linkOrUrl;
        } else if (linkOrUrl && linkOrUrl.getAttribute) {
            // Prefer data-url when available (handles anchors with href="javascript:void(0)")
            url = linkOrUrl.dataset && linkOrUrl.dataset.url ? linkOrUrl.dataset.url : linkOrUrl.getAttribute('href');
            if (event) event.preventDefault();
        } else {
            return;
        }

        // Handle orderId parameter
        if (orderId && url.indexOf('?') === -1) {
            url = url + '?id=' + orderId;
        } else if (orderId) {
            url = url + '&id=' + orderId;
        }

        // Hiển thị loading 
        modalContent.innerHTML = `
            <div class="modal-body text-center py-5">
                <div class="spinner-border text-primary"></div>
                <div class="mt-2">Đang tải...</div>
            </div>`;

        let modal = bootstrap.Modal.getInstance(modalEl);
        if (!modal) modal = new bootstrap.Modal(modalEl);

        modal.show();

        fetch(url)
            .then(res => res.text())
            .then(html => {
                modalContent.innerHTML = html;
            })
            .catch(() => {
                modalContent.innerHTML = `<div class="text-danger p-3">Lỗi tải dữ liệu</div>`;
            });
    };

    // ================= SUBMIT FORM TRONG MODAL =================
    modalContent.addEventListener("submit", async function (e) {
        const form = e.target;
        if (!(form instanceof HTMLFormElement)) return;

        e.preventDefault();

        const url = form.action;
        const fd = new FormData(form);
        const body = new URLSearchParams(fd);

        try {
            const res = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: body.toString(),
                credentials: 'same-origin'
            });

            const data = await res.json();

            if (!data.success) {
                alert(data.message);
                return;
            }

            bootstrap.Modal.getInstance(modalEl).hide();

            if (data.redirectUrl) {
                window.location.href = data.redirectUrl;
            } else {
                location.reload();
            }

        } catch (err) {
            alert("Lỗi hệ thống!");
            console.error(err);
        }
    });

})();

window.paginationSearch = function (event, form, page) {
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
                    <span>Đang tải dữ liệu...</span>
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
        }
    })
    .catch(() => {
        if (targetEl) {
            targetEl.innerHTML = `
                <div class="text-danger">
                    Không tải được dữ liệu
                </div>`;
        }
    });
};