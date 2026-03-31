using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CredentialProviderAPP.Services.Sms;

public static class SmsMfaService
{
    // login → (codigo, expira)
    private static readonly ConcurrentDictionary<string, (string Codigo, DateTime Expira)> _codigos = new();

    public static async Task EnviarCodigoAsync(string login, string phoneNumber)
    {
        string codigo = GerarCodigo();
        DateTime expira = DateTime.UtcNow.AddMinutes(5);

        _codigos[login.ToLowerInvariant()] = (codigo, expira);

        var provider = SmsProviderFactory.Create();
        string mensagem = $"Seu código de verificação é: {codigo}. Válido por 5 minutos.";
        await provider.SendAsync(phoneNumber, mensagem);
    }

    public static (bool PodeEnviar, int SegundosRestantes) VerificarReenvio(string login)
    {
        string key = login.ToLowerInvariant();

        if (!_codigos.TryGetValue(key, out var entrada))
            return (true, 0); // não tem código — pode enviar

        int segundosRestantes = (int)(entrada.Expira - DateTime.UtcNow).TotalSeconds;

        if (segundosRestantes <= 0)
            return (true, 0); // expirou — pode enviar

        // só bloqueia se tiver menos de 4 minutos restantes (enviou há menos de 1 min)
        int segundosDesdeEnvio = 300 - segundosRestantes;
        if (segundosDesdeEnvio < 60)
            return (false, 60 - segundosDesdeEnvio);

        return (true, 0);
    }

    public static bool ValidarCodigo(string login, string codigo)
    {
        string key = login.ToLowerInvariant();

        if (!_codigos.TryGetValue(key, out var entrada))
            return false;

        if (DateTime.UtcNow > entrada.Expira)
        {
            _codigos.TryRemove(key, out _);
            return false;
        }

        if (!string.Equals(entrada.Codigo, codigo.Trim(), StringComparison.Ordinal))
            return false;

        _codigos.TryRemove(key, out _); // uso único
        return true;
    }

    private static string GerarCodigo()
    {
        // criptograficamente seguro
        int num = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return num.ToString("D6");
    }
}