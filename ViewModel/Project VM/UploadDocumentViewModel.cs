using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Project_VM
{
	public class UploadDocumentViewModel
	{
		[Required]
		public int ProjectId { get; set; }

		public int? MilestoneId { get; set; }

		[Required]
		public IFormFile File { get; set; }

		[Required]
		[MaxLength(50)]
		public string Category { get; set; } // Contract, Deliverable, Reference, Other
	}
}
