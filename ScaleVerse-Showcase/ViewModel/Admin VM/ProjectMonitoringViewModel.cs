using System;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// All projects overview
    /// </summary>
    public class ProjectMonitoringViewModel
    {
        public List<ProjectMonitorItem> Projects { get; set; }
        public string StatusFilter { get; set; } // "All", "Active", "Completed", "Stuck"

        // Stats
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int StuckProjects { get; set; }

        // Pagination
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public ProjectMonitoringViewModel()
        {
            Projects = new List<ProjectMonitorItem>();
            CurrentPage = 1;
        }
    }

    /// <summary>
    /// Individual project in monitoring list
    /// </summary>
    public class ProjectMonitorItem
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string ProviderName { get; set; } // Team or Company name
        public string ProviderType { get; set; } // "Team" or "Company"
        public string Status { get; set; }
        public int ProgressPercentage { get; set; }
        public decimal Budget { get; set; }
        public decimal PlatformEarnings { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public bool IsStuck { get; set; }
        public string StuckReason { get; set; }
        public int DaysSinceActivity { get; set; }

        public int TotalMilestones { get; set; }
        public int CompletedMilestones { get; set; }



        public string StatusBadge
        {
            get
            {
                if (IsStuck) return "badge bg-warning";
                return Status switch
                {
                    "Completed" => "badge bg-success",
                    "Active" => "badge bg-primary",
                    "InProgress" => "badge bg-info",
                    _ => "badge bg-secondary"
                };
            }
        }
    }

    /// <summary>
    /// Stuck projects specific view
    /// </summary>
    public class StuckProjectsViewModel
    {
        public List<ProjectMonitorItem> StuckProjects { get; set; }
        public int TotalStuck { get; set; }

        public StuckProjectsViewModel()
        {
            StuckProjects = new List<ProjectMonitorItem>();
        }
    }
}