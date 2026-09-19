using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Company_VM
{
	public class CreateCompanyViewModel
	{
		// ── Basic Info ──
		[Required(ErrorMessage = "Company name is required")]
		[MaxLength(200)]
		public string CompanyName { get; set; }

		[Required(ErrorMessage = "Registration number is required")]
		[MaxLength(100)]
		public string RegistrationNumber { get; set; }

		[MaxLength(100)]
		public string TaxId { get; set; }

		public string Description { get; set; }

		[Required(ErrorMessage = "Industry is required")]
		[MaxLength(200)]
		public string Industry { get; set; }

		[MaxLength(100)]
		public string CompanySize { get; set; } // 1-10, 11-50, 51-200, 200+

		// ── Location ──
		[MaxLength(200)]
		public string Address { get; set; }

		[MaxLength(100)]
		public string City { get; set; }

		[MaxLength(100)]
		public string Country { get; set; }

		[MaxLength(500)]
		public string Website { get; set; }

		// ── Files (uploaded as IFormFile) ──
		public IFormFile LogoFile { get; set; }
		public IFormFile RegistrationDocumentFile { get; set; }

		// ── Services ──
		public List<CreateServiceViewModel> Services { get; set; } = new();
	}
}

