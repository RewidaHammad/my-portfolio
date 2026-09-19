namespace ScalaVerse.ViewModel.Client_Dashboard_VM
{
    public class DashboardViewModel
    {
        public StatisticsViewModel Statistics { get; set; }
        public List<ProjectCardViewModel> RecentProjects { get; set; }
        public string ClientName { get; set; }
    }
}