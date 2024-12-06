namespace MyEStore.Models
{
    public class ProfileVM
    {
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
        public string? Phone { get; set; }
    }
}
