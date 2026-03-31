using System;
using System.DirectoryServices;
using System.Security.Principal;
using System.Windows;
using CredentialProviderAPP.Enums;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;
using CredentialProviderAPP.Views;
using System.IO;
using System.Net.Http;
using System.Text.Json;

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
                var login = GetStartupLogin(e.Args);

                switch (mode)
                {
                    case AppMode.CheckMfa:
                        {
                            int result = VerificarMfaSilencioso(login);
                            Environment.Exit(result);
                            return;
                        }

                    case AppMode.Mfa:
                        {
                            File.AppendAllText(@"C:\CredentialProvider\app_debug.txt",
                                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] AppMode.Mfa iniciado. login={login}{Environment.NewLine}");

                            string metodo = "app";

                            try
                            {
                                string baseUrl = ConfigHelper.Get("Server:BaseUrl");
                                string url = $"{baseUrl.TrimEnd('/')}/mfa/status?login={Uri.EscapeDataString(login)}";

                                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                var httpResp = client.GetAsync(url).GetAwaiter().GetResult();
                                string json = httpResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                                File.AppendAllText(@"C:\CredentialProvider\app_debug.txt",
                                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] status response: {json}{Environment.NewLine}");

                                using var doc = JsonDocument.Parse(json);
                                var root = doc.RootElement;

                                if (root.TryGetProperty("Metodo", out var metodoProp))
                                    metodo = metodoProp.GetString() ?? "app";
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText(@"C:\CredentialProvider\app_debug.txt",
                                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] erro ao obter metodo: {ex.Message}{Environment.NewLine}");
                            }

                            File.AppendAllText(@"C:\CredentialProvider\app_debug.txt",
                                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] metodo={metodo}, abrindo janela{Environment.NewLine}");

                            // SMS é enviado dentro do Window_Loaded da janela
                            var verificarWindow = new VerificarCodigoWindow(login, metodo);
                            bool? ok = verificarWindow.ShowDialog();

                            Environment.Exit(verificarWindow.CodigoValidado ? 0 : 1);
                            return;
                        }

                    case AppMode.Setup:
                        {
                            var mainWindow = new MainWindow(login, AppMode.Setup);
                            mainWindow.Show();
                            return;
                        }

                    case AppMode.Reset:
                        {
                            var resetWindow = new ResetSenhaWindow(login);
                            bool? ok = resetWindow.ShowDialog();

                            Environment.Exit(ok == true ? 0 : 1);
                            return;
                        }

                    case AppMode.NewPassword:
                        {
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
        private static string GetStartupLogin(string[] args)
        {
            return args != null && args.Length > 1
                ? args[1]
                : string.Empty;
        }

        private static int VerificarMfaSilencioso(string login)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(login))
                    return 3;

                string baseUrl = ConfigHelper.Get("Server:BaseUrl");
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return 3;

                string url = $"{baseUrl.TrimEnd('/')}/mfa/status?login={Uri.EscapeDataString(login)}";

                File.AppendAllText(
                    @"C:\CredentialProvider\app_debug.txt",
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] checkmfa local iniciado. Login={login} URL={url}{Environment.NewLine}"
                );

                using var handler = new HttpClientHandler();
                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = client.GetAsync(url).GetAwaiter().GetResult();
                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                File.AppendAllText(
                    @"C:\CredentialProvider\app_debug.txt",
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
                    "Configured" => 0,
                    "Pending" => 1,
                    "NotConfigured" => 2,
                    _ => 3
                };
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    @"C:\CredentialProvider\app_debug.txt",
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
    }
}