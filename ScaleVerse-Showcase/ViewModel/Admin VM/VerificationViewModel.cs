using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// List of pending team registrations
    /// </summary>
    public class PendingTeamsViewModel
    {
        public List<PendingTeamItem> PendingTeams { get; set; }
        public int TotalPending { get; set; }

        public PendingTeamsViewModel()
        {
            PendingTeams = new List<PendingTeamItem>();
        }
    }

    /// <summary>
    /// Individual pending team
    /// </summary>
    public class PendingTeamItem
    {
        public int TeamLeaderId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Bio { get; set; }
        public string ExpertiseAreas { get; set; }
        public int YearsOfExperience { get; set; }
        public string PortfolioUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DaysWaiting { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
                return $"{(int)span.TotalDays} days ago";
            }
        }
    }

    /// <summary>
    /// List of pending company registrations
    /// </summary>
    public class PendingCompaniesViewModel
    {
        public List<PendingCompanyItem> PendingCompanies { get; set; }
        public int TotalPending { get; set; }

        public PendingCompaniesViewModel()
        {
            PendingCompanies = new List<PendingCompanyItem>();
        }
    }

    /// <summary>
    /// Individual pending company
    /// </summary>
    public class PendingCompanyItem
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string ManagerName { get; set; }
        public string Email { get; set; }
        public string RegistrationNumber { get; set; }
        public string TaxId { get; set; }
        public string Industry { get; set; }
        public string CompanySize { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
        public string LogoUrl { get; set; }
        public string RegistrationDocumentUrl { get; set; }
        public int ServicesCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DaysWaiting { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
                return $"{(int)span.TotalDays} days ago";
            }
        }
    }

    /// <summary>
    /// Form for rejecting a team
    /// </summary>
    public class RejectTeamViewModel
    {
        public int TeamLeaderId { get; set; }
        public string TeamLeaderName { get; set; }

        [Required(ErrorMessage = "Rejection reason is required")]
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// Form for rejecting a company
    /// </summary>
    public class RejectCompanyViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }

        [Required(ErrorMessage = "Rejection reason is required")]
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }
    }
}