using System.ComponentModel.DataAnnotations;

namespace MyEStore.Models
{
    public class ProfileVM
    {
        public string UserName { get; set; } = null!;
        [Required(ErrorMessage = "Họ và Tên là bắt buộc.")]

        public string FullName { get; set; } = null!;
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
        public string? Phone { get; set; }
    }
}
