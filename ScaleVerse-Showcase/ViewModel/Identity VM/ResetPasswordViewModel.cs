using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Identity_VM
{
	public class ResetPasswordViewModel
	{
		[Required]
		[EmailAddress]
		public string Email { get; set; }

		[Required]
		[StringLength(100, MinimumLength = 6)]
		[DataType(DataType.Password)]
		[Display(Name = "New Password")]
		public string Password { get; set; }

		[DataType(DataType.Password)]
		[Display(Name = "Confirm Password")]
		[Compare("Password", ErrorMessage = "Passwords do not match.")]
		public string ConfirmPassword { get; set; }

		[Required]
		public string Code { get; set; }
	}
}
