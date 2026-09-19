using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Identity_VM
{
	public class ForgotPasswordViewModel : Controller
	{
		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		[Display(Name = "Email Address")]
		public string Email { get; set; }
	}
}
