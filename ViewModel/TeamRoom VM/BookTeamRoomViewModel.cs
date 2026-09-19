using Scalaverse.Entitys.Models;
using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.TeamRoom_VM
{
	public class BookTeamRoomViewModel
	{
		public int TeamRoomId { get; set; }
		public string TeamName { get; set; }
		public string TeamLeaderName { get; set; }
		public decimal HourlyRate { get; set; }
		public decimal? ProjectRate { get; set; }

		[Required(ErrorMessage = "Project name is required")]
		[StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters")]
		[Display(Name = "Project Name")]
		public string ProjectName { get; set; }

		[Required(ErrorMessage = "Project description is required")]
		[Display(Name = "Project Description")]
		public string ProjectDescription { get; set; }

		[Required(ErrorMessage = "Please select an availability slot")]
		[Display(Name = "Preferred Time Slot")]
		public int SelectedAvailabilityId { get; set; }

		[Required(ErrorMessage = "Budget is required")]
		[Range(100, 1000000, ErrorMessage = "Budget must be between $100 and $1,000,000")]
		[Display(Name = "Budget (USD)")]
		public decimal Budget { get; set; }

		[Display(Name = "Create Default Milestones")]
		public bool CreateMilestones { get; set; } = true;

		public List<TeamAvailability> AvailableSlots { get; set; }
	}
}
