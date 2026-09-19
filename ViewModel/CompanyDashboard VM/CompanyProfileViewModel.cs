using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Company_Dashboard_VM
{
    // For viewing profile
    public class CompanyProfileViewModel
    {
        public string ManagerName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Company info
        public string CompanyName { get; set; }
        public string RegistrationNumber { get; set; }
        public string TaxId { get; set; }
        public string Industry { get; set; }
        public string CompanySize { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Website { get; set; }
        public string LogoUrl { get; set; }
        public bool IsVerified { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedProjects { get; set; }
    }

    // For editing profile
    public class EditCompanyProfileViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Manager Full Name")]
        public string ManagerName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [StringLength(100)]
        [Display(Name = "Industry")]
        public string Industry { get; set; }

        [StringLength(50)]
        [Display(Name = "Company Size")]
        public string CompanySize { get; set; }

        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(200)]
        [Display(Name = "Address")]
        public string Address { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string City { get; set; }

        [StringLength(100)]
        [Display(Name = "Country")]
        public string Country { get; set; }

        [StringLength(200)]
        [Display(Name = "Website")]
        [Url]
        public string Website { get; set; }
    }
}