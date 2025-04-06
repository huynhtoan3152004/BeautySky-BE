using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BeautySky.DTO
{
    public class UpdateUserRequest
    {
        public string? UserName { get; set; }
        public string? FullName { get; set; }

        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Email không hợp lệ.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public int? RoleId { get; set; }
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Số điện thoại chỉ được chứa chữ số.")]
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Point cannot be negative")]
        [DefaultValue(0)]
        public int Point { get; set; }
    }
}