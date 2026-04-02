namespace CredentialProviderAPP.Services.Sms;

public interface ISmsProvider
{
    Task SendAsync(string phoneNumber, string message);
}