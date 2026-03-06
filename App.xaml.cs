using System.Security.Principal;
using System.Windows;
using CredentialProviderAPP.Views;

namespace CredentialProviderAPP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // execução chamada pelo Credential Provider
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

                if (mode == "reset")
                {
                    string login = e.Args.Length > 1 ? e.Args[1] : "";
                    var reset = new ResetSenhaWindow(login);
                    reset.Show();
                    return;
                }
            }

            // execução manual
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

        private bool UsuarioEhAdministrador()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}