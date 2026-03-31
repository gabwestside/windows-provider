using CredentialProviderAPP.Utils;

namespace CredentialProviderAPP.Services.Sms;

public class TwilioSmsProvider : ISmsProvider
{
    public Task SendAsync(string phoneNumber, string message)
    {
        // TODO: instalar pacote Twilio e implementar
        // string accountSid = ConfigHelper.Get("Sms:Twilio:AccountSid");
        // string authToken  = ConfigHelper.Get("Sms:Twilio:AuthToken");
        // string from       = ConfigHelper.Get("Sms:Twilio:From");
        // var client = new TwilioRestClient(accountSid, authToken);
        // await MessageResource.CreateAsync(to: new PhoneNumber(phoneNumber),
        //                                   from: new PhoneNumber(from),
        //                                   body: message);
        throw new NotImplementedException("Twilio ainda não configurado.");
    }
}