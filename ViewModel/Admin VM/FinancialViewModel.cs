using System;
using System.Collections.Generic;

namespace ScalaVerse.ViewModel.Admin_VM
{
   
    public class FinancialOverviewViewModel
    {
        // Overall Stats
        public decimal TotalRevenue { get; set; }
        public decimal TotalPlatformEarnings { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyEarnings { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageProjectValue { get; set; }

        // Current commission rate
        public decimal CommissionPercentage { get; set; }

        // Monthly breakdown
        public List<MonthlyRevenueItem> MonthlyBreakdown { get; set; }

        // Top earners
        public List<TopEarnerItem> TopTeams { get; set; }
        public List<TopEarnerItem> TopCompanies { get; set; }

        // Recent transactions
        public List<TransactionItem> RecentTransactions { get; set; }

        public FinancialOverviewViewModel()
        {
            MonthlyBreakdown = new List<MonthlyRevenueItem>();
            TopTeams = new List<TopEarnerItem>();
            TopCompanies = new List<TopEarnerItem>();
            RecentTransactions = new List<TransactionItem>();
        }
    }

    /// <summary>
    /// Monthly revenue breakdown
    /// </summary>
    public class MonthlyRevenueItem
    {
        public string Month { get; set; } // "Jan 2026"
        public decimal Revenue { get; set; }
        public decimal PlatformEarnings { get; set; }
        public int ProjectsCount { get; set; }
    }

    /// <summary>
    /// Top earning teams/companies
    /// </summary>
    public class TopEarnerItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal TotalEarned { get; set; }
        public int ProjectsCompleted { get; set; }
        public decimal AverageRating { get; set; }
    }

    /// <summary>
    /// Transaction history item
    /// </summary>
    public class TransactionItem
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string ProviderName { get; set; }
        public decimal Amount { get; set; }
        public decimal PlatformEarnings { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}