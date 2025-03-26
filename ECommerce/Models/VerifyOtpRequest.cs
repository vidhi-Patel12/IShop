namespace ECommerce.Models
{
    public class VerifyOtpRequest
    {
        public string SessionInfo { get; set; }
        public string Code { get; set; }
    }
}
