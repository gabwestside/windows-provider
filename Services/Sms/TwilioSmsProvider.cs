using CredentialProviderAPP.Utils;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace CredentialProviderAPP.Services.Sms;

public class TwilioSmsProvider : ISmsProvider
{
    public async Task SendAsync(string phoneNumber, string message)
    {
        string accountSid = ConfigHelper.Get("Sms:Twilio:AccountSid");
        string authToken  = ConfigHelper.Get("Sms:Twilio:AuthToken");
        string from       = ConfigHelper.Get("Sms:Twilio:From");

        TwilioClient.Init(accountSid, authToken);

        await MessageResource.CreateAsync(
            to:   new PhoneNumber(phoneNumber),
            from: new PhoneNumber(from),
            body: message
        );
    }
}