using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Project_VM
{
	public class SubmitReviewViewModel
	{
		[Required]
		public int ProjectId { get; set; }

		[Required]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
		public int Rating { get; set; }

		[Required(ErrorMessage = "Please provide a comment")]
		[MinLength(10, ErrorMessage = "Comment must be at least 10 characters")]
		public string Comment { get; set; }

		[Range(1, 5)]
		public int? CommunicationRating { get; set; }

		[Range(1, 5)]
		public int? QualityRating { get; set; }

		[Range(1, 5)]
		public int? TimelinessRating { get; set; }

		[Range(1, 5)]
		public int? ProfessionalismRating { get; set; }
	}
}

