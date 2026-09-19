namespace ScalaVerse.ViewModel.Profile_VM
{
    public class ProfileViewModel
    {
        // From ApplicationUser
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // From Client
        public string CompanyName { get; set; }
        public string Industry { get; set; }
        public string CompanySize { get; set; }
        public string Bio { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public bool IsVerified { get; set; }
    }
}