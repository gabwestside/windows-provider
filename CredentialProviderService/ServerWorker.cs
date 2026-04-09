using CredentialProviderAPP.Services;

namespace CredentialProviderAPP;

public class ServerWorker : BackgroundService
{
    private readonly ILogger<ServerWorker> _logger;

    public ServerWorker(ILogger<ServerWorker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CredentialProviderMFA iniciando ServerService...");

        return Task.Run(() =>
        {
            try
            {
                ServerService.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal no ServerService.");
                throw;
            }
        }, stoppingToken);
    }
}