using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.TeamRoom_VM
{
	public class BookTeamRoomPostViewModel
	{
		[Required]
		public int TeamRoomId { get; set; }

		[Required]
		public string ProjectName { get; set; }

		[Required]
		public string ProjectDescription { get; set; }

		[Required]
		[Range(1, 1000000)]
		public decimal Budget { get; set; }

		public bool CreateMilestones { get; set; }
	}

}
