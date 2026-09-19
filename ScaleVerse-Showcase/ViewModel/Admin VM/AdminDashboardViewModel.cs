using System;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// Main admin dashboard with overview statistics
    /// </summary>
    public class AdminDashboardViewModel
    {
        // Quick Stats
        public int TotalUsers { get; set; }
        public int TotalClients { get; set; }
        public int TotalTeamLeaders { get; set; }
        public int TotalCompanyManagers { get; set; }
        public int ActiveUsers { get; set; }
        public int SuspendedUsers { get; set; }

        // Verification Stats
        public int PendingTeams { get; set; }
        public int PendingCompanies { get; set; }
        public int TotalVerifications { get; set; }

        // Project Stats
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int StuckProjects { get; set; }

        // Financial Stats
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal PlatformEarnings { get; set; }

        // Recent Activity
        public List<PendingVerificationItem> RecentPendingTeams { get; set; }
        public List<PendingVerificationItem> RecentPendingCompanies { get; set; }
        public List<StuckProjectItem> RecentStuckProjects { get; set; }

        public AdminDashboardViewModel()
        {
            RecentPendingTeams = new List<PendingVerificationItem>();
            RecentPendingCompanies = new List<PendingVerificationItem>();
            RecentStuckProjects = new List<StuckProjectItem>();
        }
    }

    /// <summary>
    /// Item for pending verification preview
    /// </summary>
    public class PendingVerificationItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime RequestedAt { get; set; }
        public int DaysWaiting { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - RequestedAt;
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
                return $"{(int)span.TotalDays} days ago";
            }
        }
    }

    /// <summary>
    /// Item for stuck projects preview
    /// </summary>
    public class StuckProjectItem
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public int DaysStuck { get; set; }
        public string Status { get; set; }
    }
}