using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Profile_VM
{
    public class EditProfileViewModel
    {
        // From ApplicationUser
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

        // From Client model
        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [StringLength(100)]
        [Display(Name = "Industry")]
        public string Industry { get; set; }

        [StringLength(100)]
        [Display(Name = "Company Size")]
        public string CompanySize { get; set; }

        [StringLength(500)]
        [Display(Name = "Bio")]
        public string Bio { get; set; }

        [StringLength(200)]
        [Display(Name = "Address")]
        public string Address { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string City { get; set; }

        [StringLength(100)]
        [Display(Name = "Country")]
        public string Country { get; set; }
    }
}