namespace SecurePortal.Models
{
    public class EncryptedRecord
    {
        public int Id { get; set; }
        public string PlainText { get; set; }
        public string EncryptedText { get; set; }
        public string SecretKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}