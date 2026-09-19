namespace SecurePortal.Models
{
    public class CaptchaToken
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Code { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}