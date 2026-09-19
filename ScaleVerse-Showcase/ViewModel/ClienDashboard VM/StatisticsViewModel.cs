namespace ScalaVerse.ViewModel.Client_Dashboard_VM
{
    /// <summary>
    /// The 4 statistics numbers at top of dashboard
    /// </summary>
    public class StatisticsViewModel
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public decimal TotalSpent { get; set; }
    }
}