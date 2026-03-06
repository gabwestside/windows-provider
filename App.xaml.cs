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