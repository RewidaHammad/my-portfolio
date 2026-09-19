using System;
using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// Platform settings edit form
    /// </summary>
    public class PlatformSettingsViewModel
    {
        public int SettingsId { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Commission must be between 0% and 100%")]
        [Display(Name = "Commission Percentage (%)")]
        public decimal CommissionPercentage { get; set; }

        [Required]
        [Range(0, 1000000, ErrorMessage = "Invalid minimum budget")]
        [Display(Name = "Minimum Project Budget ($)")]
        public decimal MinProjectBudget { get; set; }

        [Required]
        [Range(0, 10000000, ErrorMessage = "Invalid maximum budget")]
        [Display(Name = "Maximum Project Budget ($)")]
        public decimal MaxProjectBudget { get; set; }

        [Required]
        [Range(1, 365, ErrorMessage = "Must be between 1 and 365 days")]
        [Display(Name = "Project Stuck Detection (days)")]
        public int ProjectStuckDays { get; set; }

        [Required]
        [Range(1, 365, ErrorMessage = "Must be between 1 and 365 days")]
        [Display(Name = "Reapplication Wait Period (days)")]
        public int ReapplicationWaitDays { get; set; }

        public DateTime UpdatedAt { get; set; }
        public string UpdatedByAdmin { get; set; }
    }
}