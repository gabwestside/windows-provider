using System;
using System.Security.Principal;
using System.Windows;
using CredentialProviderAPP.Views;

namespace CredentialProviderAPP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            try
            {
                if (e.Args.Length > 0)
                {
                    string mode = e.Args[0].ToLower();

                    if (mode == "mfa")
                    {
                        string login = e.Args.Length > 1 ? e.Args[1] : "";
                        var mainWindow = new MainWindow(login);
                        mainWindow.Show();
                        return;
                    }

                    if (mode == "setup")
                    {
                        string login = e.Args.Length > 1 ? e.Args[1] : "";
                        var mainWindow = new MainWindow(login);
                        mainWindow.Show();
                        return;
                    }

                    if (mode == "reset")
                    {
                        string login = e.Args.Length > 1 ? e.Args[1] : "";

                        var reset = new ResetSenhaWindow(login);
                        bool? ok = reset.ShowDialog();

                        if (ok == true)
                        {
                            // 🔥 depois da troca de senha → chama MFA
                            var mfa = new MainWindow(login);
                            mfa.Show();
                        }
                        else
                        {
                            Environment.Exit(1);
                        }

                        return;
                    }
                    if (mode == "newpassword")
                    {
                        string login = e.Args.Length > 1 ? e.Args[1] : "";

                        var w = new NovaSenhaWindow(login);
                        bool? ok = w.ShowDialog();

                        Environment.Exit(ok == true ? 0 : 1);
                        return;
                    }
                }

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
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao iniciar aplicação:\n\n" + ex.ToString(),
                    "Erro crítico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Erro não tratado:\n\n" + e.Exception.ToString(),
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