using System.ComponentModel.DataAnnotations;

namespace ScalaVerse.ViewModel.Admin_VM
{
    /// <summary>
    /// Form for suspending a user
    /// </summary>
    public class SuspendUserViewModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }

        [StringLength(500)]
        public string SuspensionReason { get; set; }
    }

}