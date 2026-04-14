using CredentialProviderAPP.Config;

namespace CredentialProviderAPP.Services.Sms;

public static class SmsProviderFactory
{
    public static ISmsProvider Create()
    {
        string provider = ConfigHelper.Get("Sms:Provider") ?? "File";

        return provider.Trim().ToLowerInvariant() switch
        {
            "twilio"  => new TwilioSmsProvider(),
            // "awssns"  => new AwsSnsSmsProvider(),
            "file"    => new FileSmsProvider(),
            _         => new FileSmsProvider()
        };
    }
}