using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Identity_VM
{


	public class RegisterViewModel
	{
		[Required(ErrorMessage = "Full name is required")]
		[StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; }

		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		[Display(Name = "Email Address")]
		public string Email { get; set; }

		[Phone(ErrorMessage = "Invalid phone number")]
		[Display(Name = "Phone Number")]
		public string PhoneNumber { get; set; }

		[Required(ErrorMessage = "Please select a role")]
		[Display(Name = "Register As")]
		public string UserType { get; set; }

		[Required(ErrorMessage = "Password is required")]
		[StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 6)]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string Password { get; set; }

		[DataType(DataType.Password)]
		[Display(Name = "Confirm Password")]
		[Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
		public string ConfirmPassword { get; set; }
	}
}
