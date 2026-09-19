using System.ComponentModel.DataAnnotations;

namespace SecurePortal.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "At least 6 characters")]
        public string NewPassword { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter the CAPTCHA")]
        public string CaptchaInput { get; set; }

        // no [Required] here — this field is optional now, Session handles it
        public string? CaptchaCode { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6)]
        public string NewPassword { get; set; }
    }

    public class CryptoViewModel
    {
        public string? PlainText { get; set; }
        public string? EncryptedText { get; set; }
        public string? SecretKey { get; set; }
        public string? DecryptedResult { get; set; }
    }
}