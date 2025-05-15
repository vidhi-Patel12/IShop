using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using System.Reflection;
using System.Text;

namespace ECommerce.Models
{
    public class SmsService
    {
        private const string AccountSid = "AC442e82f708bba2fce45fe704cd7c9de0"; 
        private const string AuthToken = "1686c19c6aa82395d87ac58eac94a56a"; 
        private const string FromPhoneNumber = "+19152924665"; // Twilio Sandbox Number

        public bool SendSmsOTP(long mobile, int otp)
        {
            try
            {
                TwilioClient.Init(AccountSid, AuthToken);

                string formattedMobile = $"+91{mobile}";

                var message = MessageResource.Create(
                    from: new PhoneNumber(FromPhoneNumber), // Twilio SMS number
                    to: new PhoneNumber(formattedMobile),   // Recipient phone number
                    body: $"Your OTP for login is: {otp}. It is valid for 5 minutes. Do not share this with anyone."
                );

                Console.WriteLine($"OTP sent to WhatsApp: {formattedMobile} (Message SID: {message.Sid})");
                return true;
            }
            catch (Twilio.Exceptions.ApiException ex)
            {
                Console.WriteLine($"Twilio API Exception: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex.Message}");
                return false;
            }
        }
    }
}
