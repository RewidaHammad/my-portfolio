using System;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// User management page with filters
    /// </summary>
    public class UserManagementViewModel
    {
        public List<UserListItem> Users { get; set; }
        public string SearchTerm { get; set; }
        public string RoleFilter { get; set; } // "All", "Client", "TeamLeader", "CompanyManager"
        public string StatusFilter { get; set; } // "All", "Active", "Suspended"

        // Pagination
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalUsers { get; set; }

        public UserManagementViewModel()
        {
            Users = new List<UserListItem>();
            CurrentPage = 1;
        }
    }

    /// <summary>
    /// Individual user in the list
    /// </summary>
    public class UserListItem
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsSuspended { get; set; }
        public string SuspensionReason { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Activity stats
        public int ProjectsCount { get; set; }

        public string LastLoginDisplay
        {
            get
            {
                if (!LastLoginAt.HasValue) return "Never";
                var span = DateTime.UtcNow - LastLoginAt.Value;
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
                if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";
                return LastLoginAt.Value.ToString("MMM dd, yyyy");
            }
        }

        public string StatusBadge
        {
            get
            {
                if (IsSuspended) return "badge bg-danger";
                if (IsActive) return "badge bg-success";
                return "badge bg-secondary";
            }
        }

        public string StatusText
        {
            get
            {
                if (IsSuspended) return "Suspended";
                if (IsActive) return "Active";
                return "Inactive";
            }
        }
    }
}