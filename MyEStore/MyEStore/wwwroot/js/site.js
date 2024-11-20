// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Hàm cập nhật số lượng sản phẩm trong giỏ hàng
function updateCartCount() {
    $.get("/Cart/GetCartCount", function (data) {
        $("#cart-count").text(data); // Cập nhật số trên badge
    });
}

// Gọi hàm này khi trang được tải
$(document).ready(function () {
    updateCartCount();
});
