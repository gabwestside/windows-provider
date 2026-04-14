using CredentialProviderAPP.Config;
using System.IO;

namespace CredentialProviderAPP.Services.Sms;

public class FileSmsProvider : ISmsProvider
{
    public Task SendAsync(string phoneNumber, string message)
    {
        string path = ConfigHelper.Get("Sms:File:OutputPath");

        if (string.IsNullOrWhiteSpace(path))
            path = @"C:\CredentialProvider\sms_debug.txt";

        string linha = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] Para: {phoneNumber} | Mensagem: {message}";
        File.AppendAllText(path, linha + Environment.NewLine);

        return Task.CompletedTask;
    }
}