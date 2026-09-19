using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Company_VM
{
	public class CreateServiceViewModel
	{
		[Required]
		[MaxLength(200)]
		public string ServiceName { get; set; }

		public string Description { get; set; }

		[MaxLength(50)]
		public string ServiceTier { get; set; } // Starter, Pro, Enterprise

		[Range(0, double.MaxValue)]
		public decimal StartingPrice { get; set; }

		[MaxLength(50)]
		public string PricingModel { get; set; } // Fixed, Hourly, Monthly

		[MaxLength(200)]
		public string DeliveryTime { get; set; }
	}
}

