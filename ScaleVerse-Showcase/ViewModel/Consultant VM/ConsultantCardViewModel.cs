namespace ScalaVerse.ViewModel.Consultant_VM
{
	public class ConsultantCardViewModel
	{
		public int ConsultantId { get; set; }
		public string FullName { get; set; }
		public string ProfileImageUrl { get; set; }   // ✅ اتصلح
		public string Specialty { get; set; }
		public string Industry { get; set; }
		public string ShortBio { get; set; }
		public int YearsOfExperience { get; set; }
		public decimal? AverageRating { get; set; }
		public int TotalReviews { get; set; }
		public bool IsVerified { get; set; }
		public bool IsAvailable { get; set; }
		public decimal HourlyRate { get; set; }
		public List<string> TopSkills { get; set; } = new();
	}
}

