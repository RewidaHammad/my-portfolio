using System;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// Detailed analytics and charts
    /// </summary>
    public class AdminStatisticsViewModel
    {
        // User Growth
        public List<GrowthDataPoint> UserGrowth { get; set; }
        public List<GrowthDataPoint> ProjectGrowth { get; set; }
        public List<GrowthDataPoint> RevenueGrowth { get; set; }

        // Success Metrics
        public decimal ProjectSuccessRate { get; set; }
        public decimal AverageProjectDuration { get; set; } // in days
        public decimal ClientRetentionRate { get; set; }
        public decimal ProviderRetentionRate { get; set; }

        // Platform Health
        public int ActiveUsersToday { get; set; }
        public int ActiveUsersThisWeek { get; set; }
        public int ActiveUsersThisMonth { get; set; }
        public decimal AveragePlatformRating { get; set; }

        public AdminStatisticsViewModel()
        {
            UserGrowth = new List<GrowthDataPoint>();
            ProjectGrowth = new List<GrowthDataPoint>();
            RevenueGrowth = new List<GrowthDataPoint>();
        }
    }

    /// <summary>
    /// Data point for charts
    /// </summary>
    public class GrowthDataPoint
    {
        public string Label { get; set; } // "Jan 2026"
        public decimal Value { get; set; }
    }
}