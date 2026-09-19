using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Consultant_VM
{
	public class ConsultantSetupViewModel
	{
		// ── Profile Image ──────────────────────────────
		[Display(Name = "Profile Photo")]
		public IFormFile? ProfileImage { get; set; }

		// ── Professional Info ──────────────────────────
		[Required(ErrorMessage = "Specialty is required")]
		[MaxLength(100)]
		[Display(Name = "Specialty")]
		public string Specialty { get; set; }

		[Required(ErrorMessage = "Industry is required")]
		[MaxLength(100)]
		[Display(Name = "Industry")]
		public string Industry { get; set; }

		[Required(ErrorMessage = "Bio is required")]
		[MaxLength(1000)]
		[Display(Name = "Bio")]
		public string Bio { get; set; }

		[Range(0, 60)]
		[Display(Name = "Years of Experience")]
		public int YearsOfExperience { get; set; }

		[Display(Name = "Skills & Expertise Areas")]
		public string ExpertiseAreas { get; set; }

		// ── Links ──────────────────────────────────────
		[Url][Display(Name = "LinkedIn")] public string LinkedInUrl { get; set; }
		[Url][Display(Name = "GitHub")] public string GitHubUrl { get; set; }
		[Url][Display(Name = "Portfolio")] public string PortfolioUrl { get; set; }
		[Url][Display(Name = "Certifications")] public string CertificationsUrl { get; set; }

		// ── Pricing ────────────────────────────────────
		[Range(0, 10000)]
		[Display(Name = "Hourly Rate")]
		public decimal HourlyRate { get; set; }
	}
}

