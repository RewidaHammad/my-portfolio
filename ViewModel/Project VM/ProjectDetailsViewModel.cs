using Scalaverse.Entitys.Models;

namespace ScalaVerse.ViewModel.Project_VM
{
	public class ProjectDetailsViewModel
	{
		public Project Project { get; set; }
		public List<Milestone> Milestones { get; set; } = new();
		public List<ProjectDocument> Documents { get; set; } = new();
		public List<ProjectComment> Comments { get; set; } = new();
		public List<Message> RecentMessages { get; set; } = new();

		// ── Access flags ──
		public bool IsClient { get; set; }
		public bool IsTeamLeader { get; set; }
		public bool IsCompanyManager { get; set; }

		// ── Computed stats ──
		public decimal TotalPaid { get; set; }
		public int CompletedMilestones { get; set; }
		public int TotalMilestones { get; set; }
		public int DaysRemaining { get; set; }
		public bool CanSubmitReview { get; set; }
	}
}