namespace ScalaVerse.ViewModel.Consultant_VM
{
	public class ConsultantProfileViewModel
	{
		public int ConsultantId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }   // ✅ اتصلح
        public string Specialty { get; set; }
        public string Industry { get; set; }
        public string Bio { get; set; }
        public int YearsOfExperience { get; set; }
        public List<string> ExpertiseAreas { get; set; } = new();
        public decimal HourlyRate { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsVerified { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalSessions { get; set; }
        public int TotalReviews { get; set; }
        public DateTime MemberSince { get; set; }
        public string LinkedInUrl { get; set; }
        public string GitHubUrl { get; set; }
        public string PortfolioUrl { get; set; }
        public string CertificationsUrl { get; set; }
    }
	
}

