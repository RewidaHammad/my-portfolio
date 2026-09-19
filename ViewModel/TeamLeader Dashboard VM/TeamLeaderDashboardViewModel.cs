using Scalaverse.Entitys.Models;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.TeamLeader_Dashboard_VM
{
    public class TeamLeaderDashboardViewModel
    {
        // Statistics
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal AverageRating { get; set; }

        // Incoming Requests (NEW!)
        public int PendingRequestsCount { get; set; }
        public List<ProjectRequestSummary> IncomingRequests { get; set; }

        // Recent Projects
        public List<RecentProjectSummary> RecentProjects { get; set; }

        // Team Info
        public string TeamLeaderName { get; set; }
        public List<TeamRoom> MyTeams { get; set; }

        public TeamLeaderDashboardViewModel()
        {
            IncomingRequests = new List<ProjectRequestSummary>();
            RecentProjects = new List<RecentProjectSummary>();
            MyTeams = new List<TeamRoom>();
        }

        public string WelcomeMessage
        {
            get
            {
                var hour = System.DateTime.Now.Hour;
                var greeting = hour < 12 ? "Good Morning" : hour < 18 ? "Good Afternoon" : "Good Evening";
                return $"{greeting}, {TeamLeaderName}!";
            }
        }
    }

    // Sub-classes for clean structure
    public class ProjectRequestSummary
    {
        public int RequestId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public decimal? ClientBudget { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
                return $"{(int)span.TotalDays} days ago";
            }
        }
    }

    public class RecentProjectSummary
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string Status { get; set; }
        public int ProgressPercentage { get; set; }
        public decimal Budget { get; set; }

        public string StatusBadgeClass
        {
            get
            {
                return Status switch
                {
                    "Completed" => "badge bg-success",
                    "Active" => "badge bg-primary",
                    "InProgress" => "badge bg-info",
                    "OnHold" => "badge bg-warning",
                    _ => "badge bg-secondary"
                };
            }
        }

    }

}