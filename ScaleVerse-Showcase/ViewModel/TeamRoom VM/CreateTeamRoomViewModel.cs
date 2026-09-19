using ScalaVerse.ViewModel.TeamRoom_VM;
using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel
{
	public class CreateTeamRoomViewModel
	{
		[Required(ErrorMessage = "Team name is required")]
		[Display(Name = "Team Name")]
		[StringLength(200, MinimumLength = 3)]
		public string TeamName { get; set; }

		[Required(ErrorMessage = "Description is required")]
		[Display(Name = "Team Description")]
		[StringLength(2000, MinimumLength = 50)]
		public string Description { get; set; }

		[Required(ErrorMessage = "Specialty is required")]
		[Display(Name = "Specialty/Category")]
		public string Specialty { get; set; }

		[Display(Name = "Skills & Technologies")]
		public List<string> Skills { get; set; } = new List<string>();

		[Required(ErrorMessage = "Hourly rate is required")]
		[Display(Name = "Hourly Rate ($)")]
		[Range(10, 10000)]
		public decimal HourlyRate { get; set; }

		[Display(Name = "Fixed Project Rate (Optional)")]
		[Range(100, 1000000)]
		public decimal? ProjectRate { get; set; }

		[Display(Name = "Team Members")]
		public List<TeamMemberInputModel> TeamMembers { get; set; } = new List<TeamMemberInputModel>();

		// TeamLeader Info (if creating profile)
		[Display(Name = "Your Bio")]
		[StringLength(1000)]
		public string TeamLeaderBio { get; set; }

		[Display(Name = "Years of Experience")]
		[Range(0, 50)]
		public int YearsOfExperience { get; set; }

		[Display(Name = "Areas of Expertise")]
		public List<string> ExpertiseAreas { get; set; } = new List<string>();
	}
}

