using CredentialProviderAPP.Config;
using CredentialProviderAPP.Enums;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Views;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;

namespace CredentialProviderAPP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            try
            {
                var mode = GetStartupMode(e.Args);
                var login = GetEffectiveLogin(e.Args);
                var clientMachine = GetEffectiveClientMachine(e.Args);

                switch (mode)
                {
                    case AppMode.CheckMfa:
                        {
                            int result = VerificarMfaSilencioso(login, clientMachine);
                            Environment.Exit(result);
                            return;
                        }

                    case AppMode.Mfa:
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown;

                            using var mutex = new Mutex(false, @"Global\CredentialProvider_MFA");

                            if (!mutex.WaitOne(0, false))
                            {
                                Environment.Exit(1);
                                return;
                            }

                            if (string.IsNullOrWhiteSpace(login))
                            {
                                MessageBox.Show(
                                    "Não foi possível identificar o usuário logado.",
                                    "MFA",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);

                                LogoffSessaoAtual();
                                Environment.Exit(1);
                                return;
                            }

                            _ = ExecutarFluxoMfaAsync(login, clientMachine);
                            return;
                        }

                    case AppMode.Setup:
                        {
                            var mainWindow = new MainWindow(login, AppMode.Setup, clientMachine);
                            mainWindow.Show();
                            return;
                        }

                    case AppMode.Reset:
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown;

                            using var mutex = new Mutex(false, @"Global\CredentialProvider_Reset");

                            if (!mutex.WaitOne(0, false))
                            {
                                Environment.Exit(1);
                                return;
                            }

                            var resetWindow = new ResetSenhaWindow(login);
                            bool? mfaOk = resetWindow.ShowDialog();

                            if (mfaOk != true)
                            {
                                Environment.Exit(1);
                                return;
                            }

                            var novaSenhaWindow = new NovaSenhaWindow(login);
                            bool? senhaOk = novaSenhaWindow.ShowDialog();

                            Environment.Exit(senhaOk == true ? 0 : 1);
                            return;
                        }

                    case AppMode.NewPassword:
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown;

                            using var mutex = new Mutex(false, @"Global\CredentialProvider_NewPassword");

                            if (!mutex.WaitOne(0, false))
                            {
                                Environment.Exit(1);
                                return;
                            }

                            var novaSenhaWindow = new NovaSenhaWindow(login);
                            bool? ok = novaSenhaWindow.ShowDialog();

                            Environment.Exit(ok == true ? 0 : 1);
                            return;
                        }

                    case AppMode.Server:
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown;
                            ServerService.Start();
                            return;
                        }

                    case AppMode.Default:
                    default:
                        {
                            AbrirJanelaPadrao();
                            return;
                        }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao iniciar aplicação:\n\n" + ex,
                    "Erro crítico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(3);
            }
        }

        private static string GetEffectiveClientMachine(string[] args)
        {
            string machine = args != null && args.Length > 2
                ? args[2].Trim().Trim('"')
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(machine))
                return machine;

            return Environment.MachineName;
        }

        private static AppMode GetStartupMode(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return AppMode.Default;

            return args[0].Trim().ToLowerInvariant() switch
            {
                "mfa" => AppMode.Mfa,
                "setup" => AppMode.Setup,
                "reset" => AppMode.Reset,
                "newpassword" => AppMode.NewPassword,
                "server" => AppMode.Server,
                "checkmfa" => AppMode.CheckMfa,
                _ => AppMode.Default
            };
        }

        private static string GetEffectiveLogin(string[] args)
        {
            string login = args != null && args.Length > 1
                ? args[1].Trim().Trim('"')
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(login))
                return login;

            return GetUsuarioSessaoAtual();
        }

        private static string GetUsuarioSessaoAtual()
        {
            try
            {
                string fullName = WindowsIdentity.GetCurrent()?.Name ?? string.Empty;

                if (string.IsNullOrWhiteSpace(fullName))
                    return string.Empty;

                int slashPos = fullName.IndexOf('\\');
                if (slashPos >= 0 && slashPos < fullName.Length - 1)
                    return fullName.Substring(slashPos + 1);

                int atPos = fullName.IndexOf('@');
                if (atPos > 0)
                    return fullName.Substring(0, atPos);

                return fullName.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void LogoffSessaoAtual()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "logoff.exe",
                    Arguments = "",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        private static int VerificarMfaSilencioso(string login, string clientMachine)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(login))
                    return 3;

                string baseUrl = ConfigHelper.Get("Server:BaseUrl");
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return 3;

                string url = $"{baseUrl.TrimEnd('/')}/mfa/status?login={Uri.EscapeDataString(login)}&clientMachine={Uri.EscapeDataString(clientMachine)}";

                File.AppendAllText(
                    @"C:\Temp\app_debug.txt",
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] checkmfa local iniciado. Login={login} clientMachine={clientMachine} URL={url}{Environment.NewLine}"
                );

                using var handler = new HttpClientHandler();
                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = client.GetAsync(url).GetAwaiter().GetResult();
                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                File.AppendAllText(
                    @"C:\Temp\app_debug.txt",
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] resposta HTTP status={(int)response.StatusCode} body={json}{Environment.NewLine}"
                );

                if (!response.IsSuccessStatusCode)
                    return 3;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool sucesso = root.TryGetProperty("Sucesso", out var sucessoProp) && sucessoProp.GetBoolean();
                if (!sucesso)
                    return 3;

                string status = root.TryGetProperty("Status", out var statusProp)
                    ? statusProp.GetString() ?? ""
                    : "";

                return status switch
                {
                    "Trusted" => 0,
                    "Configured" => 0,
                    "Pending" => 1,
                    "NotConfigured" => 2,
                    _ => 3
                };
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    @"C:\Temp\app_debug.txt",
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] erro checkmfa local: {ex}{Environment.NewLine}"
                );
                return 3;
            }
        }

        private void AbrirJanelaPadrao()
        {
            if (UsuarioEhAdministrador())
            {
                var adminWindow = new AdminWindow();
                adminWindow.Show();
            }
            else
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }

        private void App_DispatcherUnhandledException(
            object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Erro não tratado:\n\n" + e.Exception,
                "Erro WPF",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private bool UsuarioEhAdministrador()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void LiberarSessao()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "calc.exe", // depois você troca aqui
                    UseShellExecute = true
                });
            }
            catch
            {
                // evita crash se der erro ao abrir app
            }
        }

        private async Task ExecutarFluxoMfaAsync(string login, string clientMachine)
        {
            var loading = new LoadingWindow("Conectando ao serviço...");

            try
            {
                loading.Show();
                await Task.Delay(150); // dá tempo da UI renderizar e animar

                bool apiOnline = await ServerApiService.ServicoDisponivelAsync();

                if (!apiOnline)
                {
                    loading.Close();

                    MessageBox.Show(
                        "Serviço indisponível no momento, entre em contato com o suporte.",
                        "MFA",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    LogoffSessaoAtual();
                    Environment.Exit(1);
                    return;
                }

                loading.AtualizarMensagem("Validando status do MFA...");

                string metodo = "app";
                string baseUrl = ConfigHelper.Get("Server:BaseUrl");
                string url = $"{baseUrl.TrimEnd('/')}/mfa/status?login={Uri.EscapeDataString(login)}&clientMachine={Uri.EscapeDataString(clientMachine)}";

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                var httpResp = await client.GetAsync(url);
                string json = await httpResp.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string status = root.TryGetProperty("Status", out var statusProp)
                    ? statusProp.GetString() ?? ""
                    : "";

                if (root.TryGetProperty("Metodo", out var metodoProp))
                    metodo = metodoProp.GetString() ?? "app";

                loading.Close();

                if (string.Equals(status, "Trusted", StringComparison.OrdinalIgnoreCase))
                {
                    LiberarSessao();
                    Environment.Exit(0);
                    return;
                }

                if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    var mainWindow = new MainWindow(login, AppMode.Setup, clientMachine);
                    bool? setupOk = mainWindow.ShowDialog();

                    if (setupOk == true)
                    {
                        LiberarSessao();
                        Environment.Exit(0);
                        return;
                    }

                    LogoffSessaoAtual();
                    Environment.Exit(1);
                    return;
                }

                if (string.Equals(status, "NotConfigured", StringComparison.OrdinalIgnoreCase))
                {
                    LiberarSessao();
                    Environment.Exit(0);
                    return;
                }

                var verificarWindow = new VerificarCodigoWindow(login, metodo, clientMachine);
                bool? ok = verificarWindow.ShowDialog();

                if (verificarWindow.CodigoValidado)
                {
                    LiberarSessao();
                    Environment.Exit(0);
                    return;
                }

                LogoffSessaoAtual();
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                try
                {
                    loading.Close();
                }
                catch
                {
                }

                File.AppendAllText(@"C:\Temp\app_debug.txt",
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] erro ao iniciar fluxo MFA: {ex}{Environment.NewLine}");

                MessageBox.Show(
                    "Serviço indisponível no momento, entre em contato com o suporte.",
                    "MFA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                LogoffSessaoAtual();
                Environment.Exit(1);
            }
        }
    }
}