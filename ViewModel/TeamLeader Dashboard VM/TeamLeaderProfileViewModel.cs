using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.TeamLeader_Dashboard_VM
{
    // For viewing profile
    public class TeamLeaderProfileViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Bio { get; set; }
        public string ExpertiseAreas { get; set; }
        public int YearsOfExperience { get; set; }
        public string? PortfolioUrl { get; set; }
        public bool IsVerified { get; set; }
    }

    // For editing profile
    public class EditTeamLeaderProfileViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Bio")]
        [DataType(DataType.MultilineText)]
        public string Bio { get; set; }

        [StringLength(500)]
        [Display(Name = "Expertise Areas")]
        public string ExpertiseAreas { get; set; }

        [Display(Name = "Years of Experience")]
        public int YearsOfExperience { get; set; }

        [StringLength(500)]
        [Display(Name = "Portfolio URL")]
        [Url]
        public string? PortfolioUrl { get; set; }
    }
}